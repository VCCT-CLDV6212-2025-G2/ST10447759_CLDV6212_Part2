using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace AzureRetailHub.Services
{
    public class FunctionApiOptions { public string BaseUrl { get; set; } = ""; public string Key { get; set; } = ""; }

    public class FunctionApiClient
    {
        private readonly HttpClient _http;
        private readonly FunctionApiOptions _opts;
        public FunctionApiClient(HttpClient http, IOptions<FunctionApiOptions> opts) { _http = http; _opts = opts.Value; }

        private string U(string path) => $"{_opts.BaseUrl.TrimEnd('/')}/{path}?code={_opts.Key}";

        public async Task<string?> PostJsonAsync(string path, object payload)
        {
            var res = await _http.PostAsync(U(path),
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
            return res.IsSuccessStatusCode ? await res.Content.ReadAsStringAsync() : null;
        }

        public async Task<string?> PostFileAsync(string path, IFormFile file)
        {
            using var form = new MultipartFormDataContent();
            using var fs = file.OpenReadStream();
            form.Add(new StreamContent(fs), "file", file.FileName);
            var res = await _http.PostAsync(U(path), form);
            return res.IsSuccessStatusCode ? await res.Content.ReadAsStringAsync() : null;
        }

        public async Task<bool> DeleteAsync(string path)
        {
            var res = await _http.DeleteAsync(U(path));
            return res.IsSuccessStatusCode;
        }
    }
}
