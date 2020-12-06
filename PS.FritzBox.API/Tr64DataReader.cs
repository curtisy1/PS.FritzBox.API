using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace PS.FritzBox.API
{
    internal static class Tr64DataReader
    {
        public static async Task ReadDeviceInfoAsync(FritzDevice device)
        {
            var uri = CreateRequestUri(device);
            var httpRequest = WebRequest.CreateHttp(uri);
            httpRequest.Timeout = 10000;

            try
            {
                using var response = (HttpWebResponse)(await httpRequest.GetResponseAsync());
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    await using var responseStream = response.GetResponseStream();
                    using var responseReader = new StreamReader(responseStream, Encoding.UTF8);
                    device.ParseTR64Desc(await responseReader.ReadToEndAsync());
                }
                else
                {
                    throw new FritzDeviceException($"Failed to get device info for device {device.Location.Host}. Response {response.StatusCode} - {response.StatusDescription}.");
                }
            }
            catch (WebException ex)
            {
                throw new FritzDeviceException($"Failed to get device info for device {device.Location.Host}.", ex);
            }
        }

        private static Uri CreateRequestUri(FritzDevice device)
        {
            return new UriBuilder {
                Scheme = "http",
                Host = device.Location.Host,
                Port = device.Location.Port,
                Path = "tr64desc.xml"
            }.Uri;
        }
    }
}