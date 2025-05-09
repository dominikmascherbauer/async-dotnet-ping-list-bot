using System.Net.NetworkInformation;

namespace PingListBotConsole;

public class PingManager : IDisposable
{
    private readonly HttpPortChecker _httpPortChecker;
    private readonly Dictionary<PingTarget, CancellationTokenSource> _targets;
    private readonly int _timeoutMs;
    private int _pingDelayMs;
    
    public bool UpdateAvailable { get; set; }

    public PingManager(int pingDelayMs, int timeoutMs = 5000)
    {
        if (pingDelayMs <= 1000)
        {
            throw new ArgumentOutOfRangeException("pingDelayMs", "The ping delay must be above 1000 milliseconds to avoid building a DDOS bot.");
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
        var localCts = new CancellationTokenSource();
        _ = MonitorTarget(target, localCts);
        _targets.Add(target, localCts);
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

    public void Stop()
    {
        _targets.Values.ToList().ForEach(cts => cts.Cancel());
    }

    public void Dispose()
    {
        _targets.Values.ToList().ForEach(cts => cts.Dispose());
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

    public void RemoveTarget(string ipAddress)
    {
        var pingTarget = _targets.Keys.ToList().Find(t => t.IpAddress == ipAddress);
        if (pingTarget == null) return;
        
        _targets.TryGetValue(pingTarget, out var cts);
        cts?.Cancel();
        _targets.Remove(pingTarget);
    }
}