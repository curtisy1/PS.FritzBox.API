using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PS.FritzBox.API
{
    /// <summary>
    /// class for locating upnp devices
    /// </summary>
    [Obsolete("Use FritzDevice.LocateDevicesAsync() - Will be made internal in Version 2.0")]
    public class DeviceLocator 
    {
        #region Methods

        /// <summary>
        /// Method to start discovery
        /// </summary>
        /// <returns></returns>
        [Obsolete("Use FritzDevice.LocateDevicesAsync()")]
        public Task<ICollection<FritzDevice>> DiscoverAsync()
        {
            return this.FindDevicesAsync();            
        }

        /// <summary>
        /// Method to find fritz devices
        /// </summary>
        /// <returns>the fritz devices</returns>
        private Task<ICollection<FritzDevice>> FindDevicesAsync()
        {
            return this.DiscoverBroadcast();
        }

        /// <summary>
        /// Method to discover by broadcast
        /// </summary>
        /// <returns></returns>
        private async Task<ICollection<FritzDevice>> DiscoverBroadcast()
        {
            var broadcastTasks = new List<Task<List<FritzDevice>>>();
            // iterate through all adapters and send multicast on 
            // valid adapters
            foreach(var adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.SupportsMulticast && adapter.OperationalStatus == OperationalStatus.Up
                   && adapter.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                {
                    var properties = adapter.GetIPProperties();
                    var broadcastAddress = this.GetUnicastAddress(properties);

                    // skip if invalid address
                    if (broadcastAddress == null || broadcastAddress.Equals(IPAddress.None)|| broadcastAddress.IsIPv6LinkLocal)
                        continue;

                    broadcastTasks.Add(BeginSendReceiveAsync(broadcastAddress));     
                }
            }
            
            return (await Task.WhenAll(broadcastTasks.ToArray()))
                .SelectMany(f => f)
                .Distinct()
                .ToList();
        }

        private async Task<List<FritzDevice>> BeginSendReceiveAsync(IPAddress broadcastAddress)
        {
            using var client = new UdpClient(broadcastAddress.AddressFamily) { MulticastLoopback = false };
            var socket = client.Client;
            
            var broadcastViaIpV4 = broadcastAddress.AddressFamily == AddressFamily.InterNetwork;

            if (broadcastViaIpV4)
            {
                socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, broadcastAddress.GetAddressBytes());
            }
            else
            {
                var interfaceArray = BitConverter.GetBytes((int)broadcastAddress.ScopeId);
                socket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.MulticastInterface, interfaceArray);
            }

            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            socket.ReceiveBufferSize = Int32.MaxValue;
            client.ExclusiveAddressUse = false;
            socket.Bind(new IPEndPoint(broadcastAddress, 1901));

            var broadCast = broadcastViaIpV4 ? UpnpBroadcast.CreateIpV4Broadcast() : UpnpBroadcast.CreateIpV6Broadcast();
            if (broadcastViaIpV4)
            {
                socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(broadCast.IpAdress));
            }
            else
            {
                socket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.AddMembership, new IPv6MulticastOption(broadCast.IpAdress, broadcastAddress.ScopeId));
            }

            return await this.BroadcastAsync(client, broadCast);
        }
        
        private async Task ReceiveByDnsAsync()
        {
            bool IsLoopbackAddressValue(IPAddress ipAddress)
            {
                if (ipAddress == null)
                    return true;

                if (IPAddress.IsLoopback(ipAddress))
                    return true;

                var ipAddressValue = ipAddress.ToString();
                return string.IsNullOrEmpty(ipAddressValue) || ipAddressValue == "127.0.0.1" || ipAddressValue == "0.0.0.0" || ipAddressValue == "::1" || ipAddressValue == "::";
            }
            
            // TODO: Clarify if this would be okay.
            // This only finds the gateway in most cases, so any routers in between would be ignored
            // However for configuration, you usually want the GW router so it _should_ be fine
            var ipAddresses = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.SupportsMulticast
                             && ni.OperationalStatus == OperationalStatus.Up
                             && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(i => i.GetIPProperties().DnsAddresses)
                .Distinct()
                .Where(ipa => ipa.AddressFamily == AddressFamily.InterNetwork && !IsLoopbackAddressValue(ipa))
                .ToList();   
        }

        /// <summary>
        /// Method to safe receive
        /// </summary>
        /// <param name="client"></param>
        /// <returns></returns>
        private async Task<UdpReceiveResult> SafeReceiveAsync(UdpClient client)
        {
            try
            {
                return await client.ReceiveAsync();
            }
            catch
            {
                return new UdpReceiveResult();
            }
        }

        /// <summary>
        /// Method to execute the broadcast
        /// </summary>
        /// <param name="client">the udp client</param>
        /// <param name="broadcast">The broadcast to send to the client.</param>
        private async Task<List<FritzDevice>> BroadcastAsync(UdpClient client, UpnpBroadcast broadcast)
        {
            var duplicateCount = 0;
            var iterations = 0;
            var discoveredIpAddresses = new List<IPAddress>();
            var discoveredDevices = new List<FritzDevice>();

            do
            {
                await client.SendAsync(broadcast.Content, broadcast.ContentLenght, broadcast.IpEndPoint);

                var result = await SafeReceiveAsync(client);
                if (!discoveredIpAddresses.Contains(result.RemoteEndPoint.Address) && result.Buffer.Length > 0)
                {
                    duplicateCount = 0;
                    discoveredIpAddresses.Add(result.RemoteEndPoint.Address);
                    var response = Encoding.ASCII.GetString(result.Buffer, 0, result.Buffer.Length);

                    // create device by endpoint data
                    var device = await FritzDevice.ParseResponseAsync(result.RemoteEndPoint.Address, response);
                    if (device != null && device.Location != null && device.Location.Scheme != "unknown")
                    {
                        try
                        {
                            // fetch the device info
                            await Tr64DataReader.ReadDeviceInfoAsync(device);
                            discoveredDevices.Add(device);
                        } catch (FritzDeviceException e)
                        {
                            Console.WriteLine(e.Message);
                        }
                    }
                }
                else
                {
                    duplicateCount++;
                }

                iterations++;
            } while (duplicateCount < 5 && iterations < 10);

            return discoveredDevices;
        }

      

        /// <summary>
        /// Method to get the unicast address
        /// </summary>
        /// <param name="properties"></param>
        /// <returns></returns>
        private IPAddress GetUnicastAddress(IPInterfaceProperties properties)
        {
            var ipAddress = IPAddress.None;

            foreach (var addressInfo in properties.UnicastAddresses)
            {
                if (addressInfo.Address.AddressFamily == AddressFamily.InterNetwork
                   || (addressInfo.Address.AddressFamily == AddressFamily.InterNetworkV6 && !addressInfo.Address.IsIPv6LinkLocal))
                {
                    ipAddress = addressInfo.Address;
                    break;
                }
            }

            return ipAddress;
        }

        #endregion
    }
}
