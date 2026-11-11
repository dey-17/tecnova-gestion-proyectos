using System;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Tecnova.Proyectos.Web.Services
{
    public sealed class ApiResponse
    {
        public int Status { get; set; }
        public string Body { get; set; }
    }
    public static class ApiClient
    {
        private static readonly HttpClient Client = new HttpClient(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(25) };
        public static async Task<ApiResponse> Send(string path, string method = "GET", string json = null)
        {
            var session = HttpContext.Current.Session;
            var baseUri = new Uri(ConfigurationManager.AppSettings["ApiUrl"]);
            using (var request = new HttpRequestMessage(new HttpMethod(method), new Uri(baseUri, "api/" + path)))
            {
                request.Headers.Add("X-Tecnova-Client", "mvc");
                if (session["ApiCookie"] != null) request.Headers.Add("Cookie", (string)session["ApiCookie"]);
                if (json != null) request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                try
                {
                    using (var response = await Client.SendAsync(request))
                    {
                        System.Collections.Generic.IEnumerable<string> cookies;
                        if (response.Headers.TryGetValues("Set-Cookie", out cookies))
                        {
                            var cookie = cookies.FirstOrDefault(c => c.StartsWith("TecnovaProyectos.ApiSession=", StringComparison.Ordinal));
                            if (cookie != null) session["ApiCookie"] = cookie.Split(';')[0];
                        }
                        if ((int)response.StatusCode == 401) session.Clear();
                        return new ApiResponse { Status = (int)response.StatusCode, Body = await response.Content.ReadAsStringAsync() };
                    }
                }
                catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
                {
                    return new ApiResponse { Status = 503, Body = "{\"mensaje\":\"El servicio no está disponible. Intente nuevamente en unos momentos.\"}" };
                }
            }
        }
    }
}
