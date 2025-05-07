using System.Net;

namespace PingListBotConsole;

class Program
{
    static void Main(string[] args)
    {
        var pingManager = new PingManager(5000);

        Console.Write("Enter IP addresses (comma-separated): ");
        var ipAddresses = Console.ReadLine()?.Split(',');

        if (ipAddresses != null)
        {
            foreach (var ipAddress in ipAddresses)
            {
                var trimmedIp = ipAddress.Trim();
                pingManager.RegisterTarget(trimmedIp, []);
            }
        }

        _ = pingManager.StartMonitoring();

        var consoleInterface = new ConsoleInterface();
        var running = true;
        // trigger first render
        pingManager.UpdateAvailable = true;

        // Main update loop
        while (running)
        {
            if (pingManager.UpdateAvailable)
            {
                pingManager.UpdateAvailable = false;
                
                // Update components
                consoleInterface.Update(0, pingManager.GetPingDelay(), pingManager.GetTargets());

                // Draw everything
                consoleInterface.Draw();
            }

            // Check for user inputs
            if (Console.KeyAvailable)
            {
                switch (Console.ReadKey(true).Key)
                {
                    case ConsoleKey.Q:
                        running = false;
                        pingManager.Stop();
                        break;
                    case ConsoleKey.Add:
                        pingManager.IncreasePingDelay();
                        pingManager.UpdateAvailable = true;
                        break;
                    case ConsoleKey.Subtract:
                        pingManager.DecreasePingDelay();
                        pingManager.UpdateAvailable = true;
                        break;
                    case ConsoleKey.T:
                        Console.Write("Enter IP addresses: ");
                        var ipAddress = Console.ReadLine();
                        if (ipAddress != null && IPAddress.TryParse(ipAddress, out var ip))
                        {
                            if (pingManager.GetTargets().Any(t => t.IpAddress == ipAddress))
                            {
                                pingManager.RemoveTarget(ipAddress);
                            }
                            else
                            {
                                pingManager.RegisterTarget(ipAddress, []);
                            }
                            pingManager.UpdateAvailable = true;
                        }
                        else
                        {
                            Console.WriteLine($"Invalid IP address: {ipAddress}");
                        }
                        break;
                    case ConsoleKey.P:
                        Console.Write("Enter Port Number: ");
                        var port = Console.ReadLine();
                        if (int.TryParse(port, out var portNumber) && portNumber is > 0 and < 65535 )
                        {
                            if (pingManager.GetTargets().All(t => t.CheckedPorts.Contains(portNumber)))
                            {
                                pingManager.RemoveCheckedPort(portNumber);
                            }
                            else
                            {
                                pingManager.AddCheckedPort(portNumber);
                            }
                            pingManager.UpdateAvailable = true;
                        }
                        else
                        {
                            Console.WriteLine($"Invalid port number: {port}");
                        }
                        break;
                }
            }

            // Wait before next update
            Thread.Sleep(100);
        }
    }
}