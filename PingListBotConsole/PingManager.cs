using System.Net.NetworkInformation;

namespace PingListBotConsole;

public class PingManager : IDisposable
{
    private readonly HttpPortChecker _httpPortChecker;
    private readonly Dictionary<PingTarget, (CancellationTokenSource cts, Task task)> _targets;
    private readonly int _timeoutMs;
    private int _pingDelayMs;
    
    public bool UpdateAvailable { get; set; }

    public PingManager(int pingDelayMs, int timeoutMs = 5000)
    {
        if (pingDelayMs <= 1000)
        {
            throw new ArgumentOutOfRangeException("pingDelayMs", "The ping delay is limited to at least 1000 milliseconds.");
        }
        
        _httpPortChecker = new HttpPortChecker(pingDelayMs);
        _targets = [];
        _pingDelayMs = pingDelayMs;
        _timeoutMs = timeoutMs;
    }

    public void RegisterTarget(string ipAddress, List<int> checkedPorts)
    {
        if (checkedPorts.Count == 0)
        {
            checkedPorts.Add(80);
            checkedPorts.Add(8080);
        }

        var target = new PingTarget(ipAddress, checkedPorts);
        var cts = new CancellationTokenSource();
        var task = MonitorTarget(target, cts);
        _targets.Add(target, (cts, task));
    }

    private async Task MonitorTarget(PingTarget target, CancellationTokenSource cts)
    {
        while (!cts.IsCancellationRequested)
        {
            // Ping a target 
            var pingReply = await new Ping().SendPingAsync(target.IpAddress, TimeSpan.FromMilliseconds(_timeoutMs), cancellationToken: cts.Token);

            // after pinging check if successful and update the pingTarget
            if (pingReply.Status == IPStatus.Success)
            {
                target.IsReachable = true;
                target.LastResponseTime = pingReply.RoundtripTime;
                target.LastSuccessfulPing = DateTime.UtcNow;
                target.SuccessCount++;
                // also check all ports asynchronously
                var tasks = target.CheckedPorts.Select(async port =>
                {
                    var portOpen = await _httpPortChecker.CheckPortAsync(target.IpAddress, port);
                    target.SetIsOpen(port, portOpen);
                    return portOpen;
                });

                await Task.WhenAll(tasks);
            }
            else
            {
                target.IsReachable = false;
                target.FailureCount++;
            }

            UpdateAvailable = true;
            
            await Task.Delay(_pingDelayMs, cts.Token);
        }
    }

    public async Task Stop()
    {
        var tasks = _targets.Values.Select(async pingTask =>
        {
            await pingTask.cts.CancelAsync();
            await pingTask.task;
        });
        _targets.Clear();
        
        await Task.WhenAll(tasks);
    }

    public void Dispose()
    {
        _targets.Values.ToList().ForEach(pingTarget => pingTarget.cts.Dispose());
    }

    public List<PingTarget> GetTargets()
    {
        return _targets.Keys.ToList();
    }

    public void IncreasePingDelay()
    {
        _pingDelayMs += 500;
    }

    public void DecreasePingDelay()
    {
        if (_pingDelayMs > 1000)
        {
            _pingDelayMs -= 500;
        }
    }

    public int GetPingDelay()
    {
        return _pingDelayMs;
    }

    public void AddCheckedPort(int port)
    {
        _targets.Keys.ToList().ForEach(t => t.AddCheckedPort(port));
    }

    public void RemoveCheckedPort(int port)
    {
        _targets.Keys.ToList().ForEach(t => t.RemoveCheckedPort(port));
    }

    public async Task RemoveTarget(string ipAddress)
    {
        var pingTarget = _targets.Keys.ToList().Find(t => t.IpAddress == ipAddress);
        if (pingTarget == null) return;
        
        // fetch task
        _targets.TryGetValue(pingTarget, out var pingTask);
        // remove from target list
        _targets.Remove(pingTarget);
        
        // cancel the ping task
        await pingTask.cts.CancelAsync();
        // wait for the task to finish
        await pingTask.task;
    }
}