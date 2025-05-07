namespace PingListBotConsole;

public class PingTarget
{
    public PingTarget(string ipAddress, List<int> checkedPorts)
    {
        IpAddress = ipAddress;
        IsReachable = false;
        LastResponseTime = -1;
        LastSuccessfulPing = DateTime.MinValue;
        SuccessCount = 0;
        FailureCount = 0;
        _openPorts = checkedPorts.ToDictionary(port => port, _ => false);
    }

    public string IpAddress { get; set; }
    public bool IsReachable { get; set; }
    public long LastResponseTime { get; set; }
    public DateTime LastSuccessfulPing { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    
    private readonly Dictionary<int, bool> _openPorts;

    public List<int> CheckedPorts => _openPorts.Keys.ToList();
    
    public bool IsOpen(int port)
    {
        return _openPorts.ContainsKey(port) && _openPorts[port];
    }

    public void SetIsOpen(int port, bool open)
    {
        if (_openPorts.ContainsKey(port))
        {
            _openPorts[port] = open;
        }
    }

    public void AddCheckedPort(int port)
    {
        _openPorts.TryAdd(port, false);
    }
    
    public void RemoveCheckedPort(int port)
    {
        _openPorts.Remove(port);
    }
    
    public override string ToString()
    {
        return $"{IpAddress} | {IsReachable} | {string.Join(',', _openPorts.Where(port => port.Value).Select(port => port.Key))} | {string.Join(',', _openPorts.Where(port => !port.Value).Select(port => port.Key))}";
    }
}