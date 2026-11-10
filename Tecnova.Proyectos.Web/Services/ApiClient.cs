using System;
using System.Configuration;
using System.Net.Http;
using System.Threading.Tasks;

namespace Tecnova.Proyectos.Web.Services
{
    public static class ApiClient
    {
        private static readonly HttpClient Client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(20)
        };

        public static async Task<string> Estado()
        {
            var baseUri = new Uri(ConfigurationManager.AppSettings["ApiUrl"]);
            using (var response = await Client.GetAsync(new Uri(baseUri, "api/estado")))
            {
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
        }
    }
}
