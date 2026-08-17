using AUBServicesLayer.Params;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using System.Net.Http.Headers;

namespace invetoryBackGroundServices.Params
{
    public class PrintParams
    {
        public string  ip { get; set; }
        public string port { get; set; }
        public int feederId    { get; set; }
        public int hooperId { get; set; }
        public string cardHolderName   { get; set; }
        public ProductVM product { get; set; }

        public PrintConfiguration printConfiguration { get; set; }
        public string userName { get; set; }
        public Branch branch { get; set; }
        public  string token { get; set; }
    }
}
