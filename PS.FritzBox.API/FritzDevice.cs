using PS.FritzBox.API.Base;
using PS.FritzBox.API.LANDevice;
using PS.FritzBox.API.WANDevice;
using PS.FritzBox.API.WANDevice.WANConnectionDevice;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace PS.FritzBox.API
{
    /// <summary>
    /// class representing a fritz device
    /// </summary>
    public class FritzDevice
    {
        internal FritzDevice()
        {
        }

        internal FritzDevice(IPAddress address, Uri location)
        {
            if(address == null)
            {
                throw new ArgumentNullException(nameof(address));
            }

            if(location == null)
            {
                throw new ArgumentNullException(nameof(location));
            }

            Location = location;
            IPAddress = address;
        }

        /// <summary>
        /// Method to parse the udp response
        /// </summary>
        /// <param name="address"></param>
        /// <param name="response"></param>
        /// <returns></returns>
        internal static async Task<FritzDevice> ParseResponseAsync(IPAddress address, string response)
        {
            var device = new FritzDevice { IPAddress = address };
            device.Location = device.ParseResponseAsync(response);

            var uriBuilder = new UriBuilder {
                Scheme = "http",
                Host = address.ToString(),
                Port = device.Location.Port
            };

            var securityPort = await new DeviceInfoClient(uriBuilder.Uri.ToString(), 10000).GetSecurityPortAsync();
            if (!securityPort.HasValue)
            {
                // this can happen when there's another device connected that receives broadcasts. I.e. Philips Hue Bridge
                // since it's not a Fritz device, we can safely ignore it
                return null;
            }
            
            uriBuilder.Port = securityPort.Value;
            uriBuilder.Scheme = "https";
            device.BaseUrl = uriBuilder.ToString();

            if (device.Location == null)
                return null;
            
            return device;
        }

        /// <summary>
        /// Method to parse the response
        /// </summary>
        /// <param name="response">the response</param>
        private Uri ParseResponseAsync(string response)
        {
            var values = response.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                                 .Skip(1)
                                                 .Select(line => line.Split(new[] { ":" }, 2, StringSplitOptions.None))
                                                 .Where(parts => parts.Length == 2)
                                                 .ToDictionary(parts => parts[0].ToLowerInvariant().Trim(), parts => parts[1].Trim());

            if (values.ContainsKey("location"))
            {
                var location = values["location"];
                
                var uri = Uri.TryCreate(location, UriKind.Absolute, out var locationUri) ? locationUri : new UriBuilder() { Scheme = "unknown", Host = location }.Uri;
                this.Port = uri.Port;

                return uri;
            }
            
            return null;
        }

        /// <summary>
        /// Gets the device type
        /// </summary>
        public string DeviceType { get; internal set; }

        /// <summary>
        /// Gets the friendly name
        /// </summary>
        public string FriendlyName { get; internal set; }

        /// <summary>
        /// Gets the manufacturer
        /// </summary>
        public string Manufacturer { get; internal set; }

        /// <summary>
        /// Gets the model name
        /// </summary>
        public string ModelName { get; internal set; }

        /// <summary>
        /// Gets the model description
        /// </summary>
        public string ModelDescription { get; internal set; }

        /// <summary>
        /// Gets the manufacturer url
        /// </summary>
        public string ManufacturerUrl { get; internal set; }

        /// <summary>
        /// Gets the ip address
        /// </summary>
        public IPAddress IPAddress { get; internal set; }

        /// <summary>
        /// Gets the port
        /// </summary>
        public int Port { get; internal set; }

        /// <summary>
        /// Gets or sets the location
        /// </summary>
        public Uri Location { get; set; }

        /// <summary>
        /// Gets the model number
        /// </summary>
        public string ModelNumber { get; internal set; }

        /// <summary>
        /// Gets the udn
        /// </summary>
        public string UDN { get; internal set; }

        /// <summary>
        /// Gets or sets the credentials
        /// </summary>
        public NetworkCredential Credentials { get; set; }

        /// <summary>
        /// timeout for service requests
        /// </summary>
        public int RequestTimeout { get; set; } = 10000;

        /// <summary>
        /// Gets the base url
        /// </summary>
        public string BaseUrl { get; private set; }

        /// <summary>
        /// Method the get instance of service client
        /// </summary>
        /// <typeparam name="T">the type param</typeparam>
        /// <returns>the instance of the service client</returns>
        public  T GetServiceClient<T>()
        {               
            var settings = new ConnectionSettings()
            {
                UserName = this.Credentials?.UserName,
                Password = this.Credentials?.Password,
                Timeout = this.RequestTimeout,
                BaseUrl = this.BaseUrl
            };

            return (T)Activator.CreateInstance(typeof(T), settings);
        }

        /// <summary>
        /// Method to parse the fritz tr64 description
        /// </summary>
        /// <param name="data">the description data</param>
        internal void ParseTR64Desc(string data)
        {
            var document = XDocument.Parse(data);
            var deviceRoot = this.GetElement(document.Root, "device");
            
            if (deviceRoot != null)
            {
                // read device info
                this.DeviceType = this.GetElementValue(deviceRoot, "deviceType");
                this.FriendlyName = this.GetElementValue(deviceRoot, "friendlyName");
                this.Manufacturer = this.GetElementValue(deviceRoot, "manufacturer");
                this.ManufacturerUrl = this.GetElementValue(deviceRoot, "manufacturerURL");
                this.ModelName = this.GetElementValue(deviceRoot, "modelName");
                this.ModelDescription = this.GetElementValue(deviceRoot, "modelDescription");
                this.ModelNumber = this.GetElementValue(deviceRoot, "modelNumber");
                this.UDN = this.GetElementValue(deviceRoot, "UDN");
            }
        }

        /// <summary>
        /// Mehtod to get an element
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        private XElement GetElement(XElement parent, string key)
        {
            return parent.Element(parent.Document.Root.Name.Namespace + key);
        }

        /// <summary>
        /// Method to get an element value
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        private string GetElementValue(XElement parent, string key)
        {
            return this.GetElement(parent, key).Value;
        }

        /// <summary>
        /// Method to get element collection
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        private IEnumerable<XElement> GetElements(XElement parent, string key)
        {
            return parent.Elements(parent.Document.Root.Name.Namespace + key);
        }

        /// <summary>
        /// Method to locate devices
        /// </summary>
        /// <returns>a collection of all found devices</returns>
        public static async Task<ICollection<FritzDevice>> LocateDevicesAsync()
        {
            return await new DeviceLocator().DiscoverAsync();
        }
    }
}
