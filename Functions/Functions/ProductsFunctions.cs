using System.Net;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.WebUtilities;   // <-- add
using Microsoft.Net.Http.Headers;          // <-- add
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using STJ = System.Text.Json;


namespace AzureRetailHub.Functions.Functions;

public class ProductsFunctions
{
    private readonly IConfiguration _config;
    public ProductsFunctions(IConfiguration config) => _config = config;

    [Function("ProductImageUpload")]
    public async Task<HttpResponseData> UploadImageAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "products/image")] HttpRequestData req)
    {
        // Validate Content-Type and get boundary
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

        // Blob container
        var cs = _config["StorageOptions:ConnectionString"];
        var containerName = _config["StorageOptions:BlobContainer"];
        var container = new BlobServiceClient(cs).GetBlobContainerClient(containerName);
        await container.CreateIfNotExistsAsync();

        // Read multipart
        var reader = new MultipartReader(boundary, req.Body);
        MultipartSection? section;
        string? blobUrl = null;

        while ((section = await reader.ReadNextSectionAsync()) != null)
        {
            if (!ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var cd)) continue;

            var isFile = cd.DispositionType.Equals("form-data") && (cd.FileName.HasValue || cd.FileNameStar.HasValue);
            if (!isFile) continue;

            var fileName = cd.FileName.Value ?? cd.FileNameStar.Value ?? "upload.bin";
            var blob = container.GetBlobClient($"{Guid.NewGuid()}_{fileName}");

            // Buffer to a seekable stream for reliable retries
            using var ms = new MemoryStream();
            await section.Body.CopyToAsync(ms);
            ms.Position = 0;

            var headers = new Azure.Storage.Blobs.Models.BlobHttpHeaders
            {
                ContentType = section.ContentType ?? "application/octet-stream"
            };

            await blob.UploadAsync(ms, new Azure.Storage.Blobs.Models.BlobUploadOptions
            {
                HttpHeaders = headers
            });

            blobUrl = blob.Uri.ToString();
        }


        var resp = req.CreateResponse(blobUrl is null ? HttpStatusCode.BadRequest : HttpStatusCode.OK);
        await resp.WriteStringAsync(blobUrl ?? "No file uploaded");
        return resp;
    }

    [Function("ProductUpsert")]
    public async Task<HttpResponseData> UpsertAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", "put", Route = "products")] HttpRequestData req)
    {
        var payload = await new StreamReader(req.Body).ReadToEndAsync();
        var dto = STJ.JsonSerializer.Deserialize<ProductDto>(
         payload,
         new STJ.JsonSerializerOptions { PropertyNameCaseInsensitive = true }
     );


        if (dto is null || string.IsNullOrWhiteSpace(dto.RowKey) || string.IsNullOrWhiteSpace(dto.Name))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Invalid product payload");
            return bad;
        }

        var cs = _config["StorageOptions:ConnectionString"];
        var tableName = _config["StorageOptions:ProductsTable"];
        var table = new TableClient(cs, tableName);
        await table.CreateIfNotExistsAsync();

        var entity = new TableEntity("PRODUCT", dto.RowKey)
        {
            ["Name"] = dto.Name,
            ["Description"] = dto.Description ?? "",
            ["Price"] = (double)dto.Price,
            ["ImageUrl"] = dto.ImageUrl ?? ""
        };
        await table.UpsertEntityAsync(entity, TableUpdateMode.Replace);

        var ok = req.CreateResponse(HttpStatusCode.OK);
        await ok.WriteStringAsync("Product upserted.");
        return ok;
    }
}

public record ProductDto(string RowKey, string Name, string? Description, decimal Price, string? ImageUrl);
