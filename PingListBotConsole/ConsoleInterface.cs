namespace PingListBotConsole;

using System;
using System.Runtime.InteropServices;

public class ConsoleInterface
{
    [DllImport("libc")]
    private static extern int ioctl(int fd, uint request, ref TerminalSize data);

    [StructLayout(LayoutKind.Sequential)]
    private struct TerminalSize
    {
        public ushort ws_row;
        public ushort ws_col;
        public uint ws_xpixel;
        public uint ws_ypixel;
    }

    private const uint TIOCGWINSZ = 0x5413;
    private readonly List<string> _header;
    private readonly List<PingTarget> _content;
    private readonly List<string> _footer;
    private readonly int _width;
    private readonly int _height;
    
    private readonly Dictionary<string, int> _columns;

    public ConsoleInterface()
    {
        // Get terminal size
        var terminalSize = GetTerminalSize();
        _width = terminalSize.ws_col;
        _height = terminalSize.ws_row;
        _columns = new Dictionary<string, int>
        {
            {"IP Address", "255.255.255.255".Length},
            {"Reachable", "Reachable".Length},
            {"Response Time", "Response Time".Length},
            {"Http Ports", "Http Ports".Length},
            {"Last Response", DateTime.UtcNow.ToString("HH:mm:ss - dd.MM.yyyy").Length}
        };
        _header = [];
        _content = [];
        _footer = [];

        // Enter raw mode
        Console.OpenStandardInput();
        Console.SetIn(new StreamReader(Console.OpenStandardInput()));
        Console.TreatControlCAsInput = true;
    }

    private static TerminalSize GetTerminalSize()
    {
        var size = new TerminalSize();
        ioctl(0, TIOCGWINSZ, ref size);
        return size;
    }

    public void Update(int scrollPos, int pingDelayMs, List<PingTarget> pingTargets, string? warning)
    {
        _header.Clear();
        _content.Clear();
        _footer.Clear();
        
        // update ports column size
        _columns["Http Ports"] = Math.Max("Http Ports".Length, pingTargets.Max(t => string.Join(" ", t.CheckedPorts).Length));
        
        // write header
        _header.Add($"Ping List Bot Console Application - Last Update {DateTime.UtcNow}");
        _header.Add($"Current Ping Delay: {pingDelayMs} ms (+/- to inc/dec by 500ms)");
        _header.Add(new string('═', _width));
        _header.Add("");
        _header.Add(string.Join(" | ", _columns.Select(c => c.Key.PadRight(c.Value))));
        _header.Add(string.Join("-+-", _columns.Select(c => new string('-', c.Value))));
        
        // write footer
        _footer.Add(new string('═', _width));
        _footer.Add("Press 'T' to add/remove a target");
        _footer.Add("Press 'P' to add/remove a checked http port");
        _footer.Add("Press 'Q' to quit");

        if (warning != null)
        {
            _footer.Add("");
            _footer.Add($"!!! {warning} !!!");
        }
        
        
        
        // add content
        var additionalLines = _header.Count + _footer.Count + pingTargets.Count - _height;
        var printedTargets = pingTargets.OrderBy(t =>
        {
            var octets = t.IpAddress.Split('.').Select(int.Parse).ToArray();
            return (octets[0], octets[1], octets[2], octets[3]);
        }).AsEnumerable();
        if (scrollPos > 0 && additionalLines > 0)
        {
            printedTargets = pingTargets.Skip(Math.Min(scrollPos, additionalLines));
        }
        _content.AddRange(printedTargets.Take(_height).ToList().AsReadOnly());
        
    }

    public void Draw()
    {
        Console.Clear();
        for (var row = 0; row < _height; row++)
        {
            if (row < _header.Count)
            {
                Console.WriteLine(_header[row]);
            } else if (row >= _height - _footer.Count)
            {
                Console.WriteLine(_footer[^(_height - row)]);
            }
            else
            {
                if (row - _header.Count < _content.Count)
                {
                    var pingTarget = _content[row - _header.Count];
                    Console.Write(pingTarget.IpAddress.PadLeft(_columns["IP Address"]));
                    Console.Write(" | ");
                    Console.ForegroundColor = pingTarget.IsReachable ? ConsoleColor.Green : ConsoleColor.Red;
                    Console.Write((pingTarget.IsReachable ? "\u2713" : "x").PadLeft(_columns["Reachable"]/2).PadRight(_columns["Reachable"]));
                    Console.ResetColor();
                    Console.Write(" | ");
                    Console.Write((pingTarget.IsReachable ? $"{pingTarget.LastResponseTime}" : "-").PadLeft(_columns["Response Time"] - 3));
                    Console.Write(" ms | ");
                    var padding =  _columns["Http Ports"] + 1; // +1 for the extra padding to the right
                    pingTarget.CheckedPorts.Order().ToList().ForEach(port =>
                    {
                        Console.ForegroundColor = pingTarget.IsOpen(port) ? ConsoleColor.Green : ConsoleColor.Red;
                        var str = $"{port} ";
                        Console.Write($"{port} ");
                        Console.ResetColor();
                        padding -= str.Length;
                    });
                    if (padding > 0)
                    {
                        Console.Write(new string(' ', padding));
                    }
                    Console.Write("| ");
                    Console.Write(pingTarget.SuccessCount > 0 ? pingTarget.LastSuccessfulPing.ToLocalTime().ToString("HH:mm:ss - dd.MM.yyyy") : "-".PadLeft(_columns["Last Response"]));
                    Console.WriteLine();
                }
                else
                {
                    Console.WriteLine();
                }
            }
        }
    }
}