using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json.Nodes;
using System.Configuration;
using System.Net.Http.Headers;
using System.Data;
using Newtonsoft.Json;
using System.Text;

namespace MATICA_S3300e.CLS
{
    public static class API_HttpClient
    {

        #region -> Declarations
        public static Object postContent = new Object();
        #endregion


        #region -> Private Methods
        public static async Task<HttpResponseMessage> Http_GetAsync(string url,string method,string tocken)//
        {
            try
            {
                var handler = new HttpClientHandler();
                handler.ClientCertificateOptions = ClientCertificateOption.Manual;
                handler.ServerCertificateCustomValidationCallback = (httpRequestMessage, cert, cetChain, policyErrors) => { return true; };
                using (HttpClient client = new HttpClient(handler))
                {
                    client.BaseAddress = new Uri(url);
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    if (tocken != null) client.DefaultRequestHeaders.Add("Authorization", "Bearer " + tocken);
                    try
                    {
                        HttpResponseMessage response = await client.GetAsync(url + method);
                        var body = await response.Content.ReadAsStringAsync();
                        Console.WriteLine(body);
                        //response.EnsureSuccessStatusCode();
                        string responseBody = await response.Content.ReadAsStringAsync();
                        client.Dispose();
                        return response;
                    }
                    catch (HttpRequestException ex)
                    {
                        throw new Exception(ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }
        public static async Task<HttpResponseMessage> Http_PostAsync(string url, string method, Object pContent, string tocken)
        {
            try
            {
                var handler = new HttpClientHandler();
                handler.ClientCertificateOptions = ClientCertificateOption.Manual;
                handler.ServerCertificateCustomValidationCallback = (httpRequestMessage, cert, cetChain, policyErrors) => { return true; };
                using (HttpClient client = new HttpClient(handler))
                {
                    client.BaseAddress = new Uri(url);
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    if (tocken != null) client.DefaultRequestHeaders.Add("Authorization", "Bearer " + tocken);
                    try
                    {
                        HttpContent content = new StringContent(JsonConvert.SerializeObject(pContent), Encoding.UTF8, "application/json");
                        HttpResponseMessage response = await client.PostAsync(url + method, content);
                     //   response.EnsureSuccessStatusCode();
                        return response;
                    }
                    catch (HttpRequestException ex)
                    {
                        throw new Exception(ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }
        public static async Task<HttpResponseMessage> Http_PutAsync(string url, string method, Object pContent, string tocken)
        {
            try
            {
                var handler = new HttpClientHandler();
                handler.ClientCertificateOptions = ClientCertificateOption.Manual;
                handler.ServerCertificateCustomValidationCallback = (httpRequestMessage, cert, cetChain, policyErrors) => { return true; };
                using (HttpClient client = new HttpClient(handler))
                {
                    client.BaseAddress = new Uri(url);
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    if (tocken != null) client.DefaultRequestHeaders.Add("Authorization", "Bearer " + tocken);
                    try
                    {
                        HttpContent content = new StringContent(JsonConvert.SerializeObject(pContent), Encoding.UTF8, "application/json");
                        HttpResponseMessage response = await client.PutAsync(url + method, content);
                    //    response.EnsureSuccessStatusCode();
                        return response;
                    }
                    catch (HttpRequestException ex)
                    {
                        throw new Exception(ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }
        #endregion
    }
}
