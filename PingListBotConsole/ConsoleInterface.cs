using System.Globalization;
using System.Net.Http.Headers;

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
            {"Last Response", DateTime.UtcNow.ToString(CultureInfo.CurrentCulture).Length}
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

    public void Update(int scrollPos, int pingDelayMs, List<PingTarget> pingTargets)
    {
        _header.Clear();
        _content.Clear();
        _footer.Clear();
        
        // update ports column size
        _columns["Http Ports"] = pingTargets.Max(t => string.Join(" ", t.CheckedPorts).Length);
        
        // write header
        _header.Add($"Ping List Bot Console Application - Last Update {DateTime.UtcNow}");
        _header.Add($"Current Ping Timeout: {pingDelayMs}ms (+/- to inc/dec)");
        _header.Add(new string('═', _width));
        _header.Add("");
        _header.Add(string.Join(" | ", _columns.Select(c => c.Key.PadRight(c.Value))));
        _header.Add(string.Join("-+-", _columns.Select(c => new string('-', c.Value))));
        
        // write footer
        _footer.Add(new string('═', _width));
        _footer.Add("Press 'T' to add/remove a target");
        _footer.Add("Press 'P' to add/remove a checked http port");
        //_footer.Add("Press 1-9 to enable/disable a column (1 -> Reachable, ...)");
        _footer.Add("Press 'Q' to quit");
        
        
        // add content
        var additionalLines = _header.Count + _footer.Count + pingTargets.Count - _height;
        if (scrollPos > 0 && additionalLines > 0)
        {
            _content.AddRange(pingTargets.Skip(Math.Min(scrollPos, additionalLines)).Take(_height).ToList().AsReadOnly());
        }
        else
        {
            _content.AddRange(pingTargets.Take(_height).ToList().AsReadOnly());
        }
        
    }

    public void Draw()
    {
        Console.Clear();
        //Console.SetCursorPosition(0, 0);
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
                    pingTarget.CheckedPorts.Order().ToList().ForEach(port =>
                    {
                        Console.ForegroundColor = pingTarget.IsOpen(port) ? ConsoleColor.Green : ConsoleColor.Red;
                        Console.Write($"{port} ");
                        Console.ResetColor();
                    });
                    Console.Write(" | ");
                    Console.Write(pingTarget.LastResponseTime);
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