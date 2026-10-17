using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace BaGet.Tests
{
    /// <summary>
    /// Drives the Razor pages as a browser would: cookies kept, redirects not followed, and
    /// each form posted with the antiforgery token its page rendered.
    /// </summary>
    internal sealed class Browser : System.IDisposable
    {
        private readonly HttpClient _client;

        public Browser(BaGetApplication app)
        {
            _client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        }

        public Task<HttpResponseMessage> GetAsync(string url) => _client.GetAsync(url);

        public async Task<string> GetPageAsync(string url)
        {
            using var response = await _client.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            return await response.Content.ReadAsStringAsync();
        }

        /// <summary>
        /// Load <paramref name="page"/>, then post <paramref name="fields"/> to
        /// <paramref name="action"/> (the page itself by default).
        /// </summary>
        public async Task<HttpResponseMessage> PostAsync(string page, Dictionary<string, string> fields, string action = null)
        {
            var html = await GetPageAsync(page);
            var token = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;

            fields["__RequestVerificationToken"] = token;

            return await _client.PostAsync(action ?? page, new FormUrlEncodedContent(fields));
        }

        public async Task RegisterAsync(string userName)
        {
            using var response = await PostAsync("account/register", new Dictionary<string, string>
            {
                { "UserName", userName },
                { "Password", "Passw0rd!" },
                { "ConfirmPassword", "Passw0rd!" },
            });

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        public void Dispose() => _client.Dispose();
    }
}
