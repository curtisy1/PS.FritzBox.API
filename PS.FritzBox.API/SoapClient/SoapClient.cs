using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Collections.Generic;
using PS.FritzBox.API.XmlHelper;

namespace PS.FritzBox.API.SOAP
{
    /// <summary>
    /// class for accessing soap actions
    /// </summary>
    internal class SoapClient
    {
        /// <summary>
        /// Method to execute the soap request
        /// </summary>
        /// <param name="parameters">the request parameters</param>
        /// <returns>the result of the call</returns>
        public async Task<XDocument> InvokeAsync(Uri uri, SoapRequestParameters parameters)
        {
            var envelope = this.CreateEnvelope(parameters);
            return await this.ExecuteAsync(envelope, uri, parameters);           
        }
        
        
        /// <summary>
        /// Method to execute the soap request
        /// </summary>
        /// <param name="parameters">the request parameters</param>
        /// <returns>the result of the call</returns>
        public ValueTask<IEnumerable<T>> InvokeAsync<T>(Uri uri, SoapRequestParameters parameters)
            where T : class
        {
            return this.ExecuteAsync<T>(this.CreateEnvelope(parameters), uri, parameters);
        }

        /// <summary>
        /// Method to create the envelope
        /// </summary>
        /// <param name="parameters">the request parameters</param>
        /// <returns></returns>
        private string CreateEnvelope(SoapRequestParameters parameters)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(@"<?xml version='1.0' encoding='UTF-8'?> 
                      <soap:Envelope xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance'
                      xmlns:xsd='http://www.w3.org/2001/XMLSchema'
                      xmlns:soap='http://schemas.xmlsoap.org/soap/envelope/'>
                      <soap:Body>");

            sb.Append($"<{parameters.Action} xmlns='{parameters.RequestNameSpace}'>");
            foreach (SoapRequestParameter parameter in parameters.Parameters)
                sb.Append($"<{parameter.ParameterName}>{parameter.ParameterValue}</{parameter.ParameterName}>");
            sb.Append($"</{parameters.Action}>");
            sb.Append(@"</soap:Body></soap:Envelope>");

            return sb.ToString();
        }


        /// <summary>
        /// Method to execute a given soap request
        /// </summary>
        /// <param name="xmlSOAP">the soap request</param>
        /// <param name="url">the soap url</param>
        /// <param name="parameters">the parameters</param>
        /// <returns></returns>
        private async Task<XDocument> ExecuteAsync(string xmlSOAP, Uri url, SoapRequestParameters parameters)
        {
            HttpClientHandler handler = new HttpClientHandler {
                ServerCertificateCustomValidationCallback = delegate { return true; },
                Credentials = parameters.Credentials
            };

            using HttpClient client = new HttpClient(handler);
            var request = new HttpRequestMessage {
                RequestUri = url,
                Method = HttpMethod.Post,
                Content = new StringContent(xmlSOAP, Encoding.UTF8, "text/xml")
            };

            request.Headers.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/xml"));
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("text/xml");
            request.Headers.Add("SOAPAction", $"{parameters.SoapAction}");
              
            HttpResponseMessage response = await client.SendAsync(request);
                
            if(!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.InternalServerError)
            {
                throw new Exception(response.ReasonPhrase);
            }

            XDocument soapResponse = XDocument.Load(await response.Content.ReadAsStreamAsync());

            this.ValidateSoapResponse(soapResponse);
            return soapResponse;
        }
        
        /// <summary>
        /// Method to execute a given soap request
        /// </summary>
        /// <param name="xmlSOAP">the soap request</param>
        /// <param name="url">the soap url</param>
        /// <param name="parameters">the parameters</param>
        /// <returns></returns>
        private async ValueTask<IEnumerable<T>> ExecuteAsync<T>(string xmlSOAP, Uri url, SoapRequestParameters parameters)
            where T : class
        {
            HttpClientHandler handler = new HttpClientHandler {
                ServerCertificateCustomValidationCallback = delegate { return true; },
                Credentials = parameters.Credentials
            };

            using HttpClient client = new HttpClient(handler);
            var request = new HttpRequestMessage {
                RequestUri = url,
                Method = HttpMethod.Post,
                Content = new StringContent(xmlSOAP, Encoding.UTF8, "text/xml")
            };

            request.Headers.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/xml"));
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("text/xml");
            request.Headers.Add("SOAPAction", $"{parameters.SoapAction}");
              
            HttpResponseMessage response = await client.SendAsync(request);
                
            if(!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.InternalServerError)
            {
                return null;
            }

            try
            {
                return await AsyncStreamToList(XmlStreamer.StreamInstances<T>(await response.Content.ReadAsStringAsync()));
            } catch (Exception e)
            {
                return null;
            }
        }

        /// <summary>
        /// Method to validate soap result if its an soap fault
        /// </summary>
        /// <param name="document">the response text</param>
        private void ValidateSoapResponse(XDocument document)
        {
            var faultNode = document.Descendants("Fault").FirstOrDefault();
            if(faultNode != null)
            {
                string faultCode = faultNode.Descendants("faultcode").First().Value;
                string faultString = faultNode.Descendants("faultstring").First().Value;

                throw new SoapFaultException(faultCode, faultString, string.Empty);
            }
        }

        private async ValueTask<List<T>> AsyncStreamToList<T>(IAsyncEnumerable<T> stream)
        {
            var materialized = new List<T>();
            await foreach (var item in stream)
            {
                materialized.Add(item);
            }

            return materialized;
        }
    }
}
