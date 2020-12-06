using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PS.FritzBox.API.CMD
{
    class Program
    {
        private static readonly Dictionary<string, ClientHandler> _clientHandlers = new();

        static async Task Main(string[] args)
        {
            Console.WriteLine("Searching for devices...");
            var devices = (await FritzDevice.LocateDevicesAsync()).ToArray();

            var deviceCount = devices.Length;
            if (deviceCount > 0)
            {
                Console.WriteLine($"Found {deviceCount} devices.");
                string input;
                int deviceIndex;
                do
                {
                    for (var i = 0; i < deviceCount; i++)
                    {
                        Console.WriteLine($"{i} - {devices[i].ModelName}");
                    }

                    input = Console.ReadLine();

                } while (!int.TryParse(input, out deviceIndex) || (deviceIndex < 0 || deviceIndex >= devices.Length));

                var selected = devices.Skip(deviceIndex).First();
                Configure(selected);

                do
                {
                    Console.Clear();
                    Console.WriteLine(" 1 - DeviceInfo");
                    Console.WriteLine(" 2 - DeviceConfig");
                    Console.WriteLine(" 3 - LanConfigSecurity");
                    Console.WriteLine(" 4 - LANEthernetInterface");
                    Console.WriteLine(" 5 - LANHostConfigManagement");
                    Console.WriteLine(" 6 . WANCommonInterfaceConfig");
                    Console.WriteLine(" 7 - WANIPPConnection");
                    Console.WriteLine(" 8 - WANPPPConnection");
                    Console.WriteLine(" 9 - AppSetup");
                    Console.WriteLine("10 - Layer3Forwarding");
                    Console.WriteLine("11 - UserInterface");
                    Console.WriteLine("12 - WLANConfiguration");
                    Console.WriteLine("13 - WLANConfiguration2");
                    Console.WriteLine("14 - WLANConfiguration3");
                    Console.WriteLine("15 - WANDSLInterfaceConfig");
                    Console.WriteLine("16 - WANEthernetLinkConfig");
                    Console.WriteLine("17 - WANDSLLinkConfig");
                    Console.WriteLine("18 - Speedtest");

                    Console.WriteLine("r - Reinitialize");
                    Console.WriteLine("q - Exit");

                    input = Console.ReadLine();
                    if (_clientHandlers.ContainsKey(input))
                        await _clientHandlers[input].Handle();
                    else if (input.ToLower() == "r")
                        Configure(selected);
                    else if (input.ToLower() != "q")
                        Console.WriteLine("invalid choice");

                } while (input.ToLower() != "q");
            }
            else
            {
                Console.WriteLine("No devices found");
                Console.ReadLine();
            }
        }

        static void Configure(FritzDevice device)
        {
            var settings = GetConnectionSettings();
            device.Credentials = new System.Net.NetworkCredential(settings.UserName, settings.Password);
            //device.GetServiceClient<DeviceInfoClient>(settings);
            InitClientHandler(device);
        }

        /// <summary>
        /// Method to get the connections ettings
        /// </summary>
        /// <returns>the connection settings</returns>
        static ConnectionSettings GetConnectionSettings()
        {
            var settings = new ConnectionSettings();
            Console.Write("User: ");
            settings.UserName = Console.ReadLine();
            Console.Write("Password: ");
            settings.Password = Console.ReadLine();

            return settings;
        }

        /// <summary>
        /// Method to initialize the client handlers
        /// </summary>
        /// <param name="settings"></param>
        static void InitClientHandler(FritzDevice device)
        {
            _clientHandlers.Clear();
            Action clearOutput = () => Console.Clear();
            Action wait = () => Console.ReadKey();
            Action<string> printOutput = (output) => Console.WriteLine(output);
            Func<string> getInput = () => Console.ReadLine();

            _clientHandlers.Add("1", new DeviceInfoClientHandler(device, printOutput, getInput, wait, clearOutput));
            _clientHandlers.Add("2", new DeviceConfigClientHandler(device, printOutput, getInput, wait, clearOutput));
            _clientHandlers.Add("3", new LanConfigSecurityHandler(device, printOutput, getInput, wait, clearOutput));
            _clientHandlers.Add("4", new LANEthernetInterfaceClientHandler(device, printOutput, getInput, wait, clearOutput));
            _clientHandlers.Add("5", new LANHostConfigManagementClientHandler(device, printOutput, getInput, wait, clearOutput));
            _clientHandlers.Add("6", new WANCommonInterfaceConfigClientHandler(device, printOutput, getInput, wait, clearOutput));
            _clientHandlers.Add("7", new WANIPConnectonClientHandler(device, printOutput, getInput, wait, clearOutput));
            _clientHandlers.Add("8", new WANPPPConnectionClientHandler(device, printOutput, getInput, wait, clearOutput));
            _clientHandlers.Add("9", new AppSetupClientHandler(device, printOutput, getInput, wait, clearOutput));
            _clientHandlers.Add("10", new Layer3ForwardingClientHandler(device, printOutput, getInput, wait, clearOutput));
            _clientHandlers.Add("11", new UserInterfaceClientHandler(device, printOutput, getInput, wait, clearOutput));
            _clientHandlers.Add("12", new WLANConfigurationClientHandler(device, printOutput, getInput, wait, clearOutput));
            _clientHandlers.Add("13", new WLANConfigurationClientHandler2(device, printOutput, getInput, wait, clearOutput));
            _clientHandlers.Add("14", new WLANConfigurationClientHandler3(device, printOutput, getInput, wait, clearOutput));
            _clientHandlers.Add("15", new WANDSLInterfaceConfigClientHandler(device, printOutput, getInput, wait, clearOutput));
            _clientHandlers.Add("16", new WANEthernetLinkConfigClientHandler(device, printOutput, getInput, wait, clearOutput));
            _clientHandlers.Add("17", new WANDSLLinkConfigClientHandler(device, printOutput, getInput, wait, clearOutput));
            _clientHandlers.Add("18", new SpeedtestClientHandler(device, printOutput, getInput, wait, clearOutput));
        }

        
    }
}
