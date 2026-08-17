using MATICA_S3300e.CLS;
using MATICA_S3300e.LAN;
using Microsoft.Extensions.Configuration;
using Nancy;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net.Http;
using System.Threading.Tasks;

namespace CLS
{
    
    public  class API_Handle
    {
        public static IConfiguration Configuration { get; set; }

        public static void Init(IConfiguration config)
        {
            Configuration = config;
        }



        #region -> API Methods
        
        public  async Task<bool> GetChaeckCardExist(string cardPan,int pID,string bName,string _apiToken)
        {
            try
            {
                string logData = string.Empty;
                string respMessage = string.Empty;
              






                HttpResponseMessage getJsonResult = await API_HttpClient.Http_GetAsync(Configuration["WebAPI"] + "api/", $"Card/CheckCardExist?maskedPan={cardPan}&productId={pID}&branchName={bName}", _apiToken);//, GLOBALS.

                string ret = await getJsonResult.Content.ReadAsStringAsync();
               
                var objects = JObject.Parse(await getJsonResult.Content.ReadAsStringAsync());
                foreach (KeyValuePair<String, JToken> app in objects)
                {
                    if(app.Key == "isSuccess")
                    {
                        bool resp = Convert.ToBoolean(app.Value);
                        if (resp == true) return true;
                    }
                    if (app.Key == "errorMessage")
                    {
                        respMessage = (String)app.Value;
                       
                        return false;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
              
                return false;
            }
        }



        public static async Task<bool> SetPrintLogAsync(string cardPAN, string chName, int branchID, string branchName, string uName, int printStatus, string message, int productId, string _apiToken)
        {
            try
            {
                string respMessage = string.Empty;
                var printLogContent = new
                {
                    clearPan = cardPAN,
                    cardHolderName = chName,
                    branchId = branchID,
                    branchName = branchName,
                    username = uName,
                    status = printStatus,
                    message = message,
                    productId = productId
                };
                HttpResponseMessage getJsonResult = await API_HttpClient.Http_PutAsync(Configuration["WebAPI"] + "api/", "Card/PrintCard", printLogContent, _apiToken);
                if (string.Compare(getJsonResult.StatusCode.ToString(), "OK", StringComparison.OrdinalIgnoreCase) != 0)
                {
                    string ErrorMsg = string.Empty;
                    var objecte = JObject.Parse(await getJsonResult.Content.ReadAsStringAsync());
                    foreach (KeyValuePair<String, JToken> app in objecte)
                    {
                        if (app.Key == "errorMessages")
                            ErrorMsg = (String)app.Value[0];
                    }
                   
                    return false;
                }
                var objects = JObject.Parse(await getJsonResult.Content.ReadAsStringAsync());
                foreach (KeyValuePair<String, JToken> app in objects)
                {
                    if (app.Key == "message")
                        respMessage = (String)app.Value;
                }
            
                return true;
            }
            catch (Exception ex)
            {
               
                return false;
            }
        }
        #endregion


    }
}
