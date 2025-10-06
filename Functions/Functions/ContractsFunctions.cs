using System.Net;
using Azure; // HttpRange
using Azure.Storage.Files.Shares;
using Microsoft.AspNetCore.WebUtilities;   // MultipartReader, MultipartSection
using Microsoft.Net.Http.Headers;          // MediaTypeHeaderValue, HeaderUtilities
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;

namespace AzureRetailHub.Functions.Functions;

public class ContractsFunctions
{
    private readonly IConfiguration _config;
    public ContractsFunctions(IConfiguration config) => _config = config;

    [Function("ContractUpload")]
    public async Task<HttpResponseData> UploadAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "contracts")] HttpRequestData req)
    {
        // Validate Content-Type
        if (!req.Headers.TryGetValues("Content-Type", out var ctVals))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Content-Type header missing.");
            return bad;
        }

        var contentType = ctVals.FirstOrDefault();
        if (!MediaTypeHeaderValue.TryParse(contentType, out var mt) ||
            !string.Equals(mt?.MediaType.Value, "multipart/form-data", StringComparison.OrdinalIgnoreCase))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Content-Type must be multipart/form-data.");
            return bad;
        }

        var boundary = HeaderUtilities.RemoveQuotes(mt!.Boundary).Value;
        if (string.IsNullOrWhiteSpace(boundary))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Missing multipart boundary.");
            return bad;
        }

        // Azure Files
        var cs = _config["StorageOptions:ConnectionString"];
        var shareName = _config["StorageOptions:FileShareName"];
        var share = new ShareClient(cs, shareName);
        await share.CreateIfNotExistsAsync();
        var root = share.GetRootDirectoryClient();

        var reader = new MultipartReader(boundary, req.Body);
        MultipartSection? section;
        var uploaded = new List<string>();

        while ((section = await reader.ReadNextSectionAsync()) != null)
        {
            if (!ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var cd)) continue;

            var isFile = cd.DispositionType.Equals("form-data") &&
                         (cd.FileName.HasValue || cd.FileNameStar.HasValue);
            if (!isFile) continue;

            var originalName = cd.FileName.Value ?? cd.FileNameStar.Value ?? "upload.bin";
            var finalName = $"{Guid.NewGuid()}_{originalName}";

            using var ms = new MemoryStream();
            await section.Body.CopyToAsync(ms);
            ms.Position = 0;

            var file = root.GetFileClient(finalName);
            await file.CreateAsync(ms.Length);
            ms.Position = 0;
            await file.UploadRangeAsync(new HttpRange(0, ms.Length), ms);

            uploaded.Add(finalName);
        }

        if (uploaded.Count == 0)
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("No file part found.");
            return bad;
        }

        var ok = req.CreateResponse(HttpStatusCode.OK);
        await ok.WriteAsJsonAsync(new { files = uploaded });
        return ok;
    }
}
