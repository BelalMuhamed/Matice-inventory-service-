using System.Net.Http.Headers;
using System.Web;

namespace invetoryBackGroundServices.Helper
{
    public class APIHelper
    {
        private readonly HttpClient _httpClient;


        public APIHelper(HttpClient httpClient)
        {
            _httpClient = httpClient;


        }


        //public async Task<TResult?> CallGetApiAsync<TResult, TParams>(string token, string endpoint, TParams queryParams)
        //{
        //    try
        //    {
        //        // Build query string from object
        //        var query = ToQueryString(queryParams);
        //        string url = $"https://localhost:7193/api/{endpoint}{query}";

        //        var request = new HttpRequestMessage(HttpMethod.Get, url);
        //        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        //        HttpResponseMessage response = await _httpClient.SendAsync(request);
        //        response.EnsureSuccessStatusCode();

        //        // Deserialize response JSON into TResult
        //        var result = await response.Content.ReadFromJsonAsync<TResult>();
        //        return result;
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"Error calling GET API: {ex.Message}");
        //        return default;
        //    }
        //}

        //private static string ToQueryString<T>(T obj)
        //{
        //    if (obj == null) return string.Empty;

        //    var type = typeof(T);
        //    if (type == typeof(string) || type.IsPrimitive)
        //    {
        //        return $"/{HttpUtility.UrlEncode(obj.ToString())}";
        //    }

        //    var properties = type.GetProperties()
        //        .Where(p => p.GetValue(obj) != null)
        //        .Select(p => $"{HttpUtility.UrlEncode(p.Name)}={HttpUtility.UrlEncode(p.GetValue(obj)!.ToString())}");

        //    return properties.Any() ? "?" + string.Join("&", properties) : string.Empty;
        //}

        public async Task<TResult?> CallGetApiAsync<TResult, TParams>(
    string token,
    string endpoint,
    TParams queryParams,
    bool treatStringAsQuery = false
)
        {
            try
            {
                var query = ToQueryString(queryParams, treatStringAsQuery);
                string url = $"https://localhost:7193/api/{endpoint}{query}";

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                HttpResponseMessage response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                return await response.Content.ReadFromJsonAsync<TResult>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calling GET API: {ex.Message}");
                return default;
            }
        }

        private static string ToQueryString<T>(T obj, bool treatStringAsQuery)
        {
            if (obj == null) return string.Empty;

            var type = typeof(T);

            if (type == typeof(string) || type.IsPrimitive)
            {
                if (treatStringAsQuery)
                    return $"?name={HttpUtility.UrlEncode(obj.ToString())}";

                return "/" + obj.ToString();
            }

            var properties = type.GetProperties()
                .Where(p => p.GetValue(obj) != null)
                .Select(p => $"{HttpUtility.UrlEncode(p.Name)}={HttpUtility.UrlEncode(p.GetValue(obj)!.ToString())}");

            return properties.Any() ? "?" + string.Join("&", properties) : string.Empty;
        }




    }
}
