using System.Net.NetworkInformation;

namespace PingListBotConsole;

public class PingManager : IDisposable
{
    private readonly HttpPortChecker _httpPortChecker;
    private readonly List<PingTarget> _targets;
    private readonly CancellationTokenSource _cts;
    private int _pingDelayMs;
    
    public bool UpdateAvailable { get; set; }

    public PingManager(int pingDelayMs)
    {
        if (pingDelayMs <= 1000)
        {
            throw new ArgumentOutOfRangeException("pingDelayMs", "The ping delay must be above 1000 milliseconds to avoid building a DDOS bot.");
        }
        
        _httpPortChecker = new HttpPortChecker(pingDelayMs);
        _cts = new CancellationTokenSource();
        _targets = [];
        _pingDelayMs = pingDelayMs;
    }

    public void RegisterTarget(string ipAddress, List<int> checkedPorts)
    {
        if (checkedPorts.Count == 0)
        {
            checkedPorts.Add(80);
            checkedPorts.Add(8080);
        }
        _targets.Add(new PingTarget(ipAddress, checkedPorts));
    }

    public async Task StartMonitoring()
    {
        while (!_cts.IsCancellationRequested)
        {
            var tasks = _targets.Select(async target =>
            {
                await CheckTarget(target);
                return target;
            });
            
            await Task.WhenAll(tasks);
            
            await Task.Delay(_pingDelayMs, _cts.Token);
        }
    }

    private async Task CheckTarget(PingTarget target)
    {
        // Ping and wait at max one ping cycle 
        var pingReply = await new Ping().SendPingAsync(target.IpAddress, _pingDelayMs);

        if (pingReply.Status == IPStatus.Success)
        {
            target.IsReachable = true;
            target.LastResponseTime = pingReply.RoundtripTime;
            target.LastSuccessfulPing = DateTime.UtcNow;
            target.SuccessCount++;
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
    }

    public void Stop()
    {
        _cts.Cancel();
    }

    public void Dispose()
    {
        _cts.Dispose();
    }

    public List<PingTarget> GetTargets()
    {
        return _targets;
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
        _targets.ForEach(t => t.AddCheckedPort(port));
    }

    public void RemoveCheckedPort(int port)
    {
        _targets.ForEach(t => t.RemoveCheckedPort(port));
    }

    public void RemoveTarget(string ipAddress)
    {
        if (_targets.Any(t => t.IpAddress == ipAddress))
        {
            _targets.Remove(_targets.Find(t => t.IpAddress == ipAddress)!);
        }
    }
}