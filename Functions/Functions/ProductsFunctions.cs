using System.Net;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.WebUtilities;   // MultipartReader
using Microsoft.Net.Http.Headers;          // MediaTypeHeaderValue / ContentDisposition
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using STJ = System.Text.Json;

namespace AzureRetailHub.Functions.Functions;

/// <summary>
/// Endpoints for product image uploads (to Blob Storage) and product upserts (to Table Storage).
/// 
/// Config keys expected:
///   - StorageOptions:ConnectionString
///   - StorageOptions:BlobContainer
///   - StorageOptions:ProductsTable
/// 
/// Notes:
/// - Image upload requires multipart/form-data with at least one file section.
/// - Upsert requires RowKey + Name; others are optional.
/// </summary>
public class ProductsFunctions
{
    private readonly IConfiguration _config;
    public ProductsFunctions(IConfiguration config) => _config = config;

    /// <summary>
    /// Uploads one file from a multipart/form-data request to Blob Storage.
    /// 
    /// Route: POST /api/products/image
    /// Headers: Content-Type: multipart/form-data; boundary=----WebKitFormBoundary...
    /// Body: form-data with a file field (any name).
    /// 
    /// Returns:
    ///   200 OK with blob URL when a file is found
    ///   400 BadRequest if Content-Type/boundary is missing or no file is present
    /// 
    /// Tip:
    ///   In Postman:
    ///     - Method: POST
    ///     - Body: form-data
    ///     - Key (type = File): "file"
    ///     - Choose your image
    /// </summary>
    [Function("ProductImageUpload")]
    public async Task<HttpResponseData> UploadImageAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "products/image")] HttpRequestData req)
    {
        // Validate Content-Type and extract boundary
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

        // Prepare blob container and ensure it exists
        var cs = _config["StorageOptions:ConnectionString"];
        var containerName = _config["StorageOptions:BlobContainer"];
        var container = new BlobServiceClient(cs).GetBlobContainerClient(containerName);
        await container.CreateIfNotExistsAsync();

        // Iterate multipart sections to find the first file
        var reader = new MultipartReader(boundary, req.Body);
        MultipartSection? section;
        string? blobUrl = null;

        while ((section = await reader.ReadNextSectionAsync()) != null)
        {
            if (!ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var cd)) continue;

            var isFile = cd.DispositionType.Equals("form-data") && (cd.FileName.HasValue || cd.FileNameStar.HasValue);
            if (!isFile) continue;

            // Create blob name with GUID prefix to avoid collisions
            var fileName = cd.FileName.Value ?? cd.FileNameStar.Value ?? "upload.bin";
            var blob = container.GetBlobClient($"{Guid.NewGuid()}_{fileName}");

            // Buffer the section stream so we can retry reliably if needed
            using var ms = new MemoryStream();
            await section.Body.CopyToAsync(ms);
            ms.Position = 0;

            // Preserve content type if supplied
            var headers = new Azure.Storage.Blobs.Models.BlobHttpHeaders
            {
                ContentType = section.ContentType ?? "application/octet-stream"
            };

            await blob.UploadAsync(ms, new Azure.Storage.Blobs.Models.BlobUploadOptions
            {
                HttpHeaders = headers
            });

            blobUrl = blob.Uri.ToString();
            // We could break after first file; loop supports multiple if you ever extend it
        }

        var resp = req.CreateResponse(blobUrl is null ? HttpStatusCode.BadRequest : HttpStatusCode.OK);
        await resp.WriteStringAsync(blobUrl ?? "No file uploaded");
        return resp;
    }

    /// <summary>
    /// Upserts a Product entity into Table Storage.
    /// 
    /// Route: POST/PUT /api/products
    /// Payload (JSON):
    /// {
    ///   "rowKey": "SKU-001",
    ///   "name": "Gaming Mouse",
    ///   "description": "6-button RGB",
    ///   "price": 499.99,
    ///   "imageUrl": "https://.../image.png"
    /// }
    /// 
    /// Returns:
    ///   200 OK - "Product upserted."
    ///   400 BadRequest - invalid or missing required fields
    /// </summary>
    [Function("ProductUpsert")]
    public async Task<HttpResponseData> UpsertAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", "put", Route = "products")] HttpRequestData req)
    {
        var payload = await new StreamReader(req.Body).ReadToEndAsync();

        // Deserialize case-insensitively to be tolerant to caller casing
        var dto = STJ.JsonSerializer.Deserialize<ProductDto>(
            payload,
            new STJ.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        // Validate minimal required fields
        if (dto is null || string.IsNullOrWhiteSpace(dto.RowKey) || string.IsNullOrWhiteSpace(dto.Name))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Invalid product payload");
            return bad;
        }

        // Prepare table and ensure it exists
        var cs = _config["StorageOptions:ConnectionString"];
        var tableName = _config["StorageOptions:ProductsTable"];
        var table = new TableClient(cs, tableName);
        await table.CreateIfNotExistsAsync();

        // Map to TableEntity (PartitionKey fixed to "PRODUCT" for consistency)
        var entity = new TableEntity("PRODUCT", dto.RowKey)
        {
            ["Name"] = dto.Name,
            ["Description"] = dto.Description ?? "",
            ["Price"] = (double)dto.Price,   // Table Storage stores double, so cast from decimal
            ["ImageUrl"] = dto.ImageUrl ?? ""
        };

        await table.UpsertEntityAsync(entity, TableUpdateMode.Replace);

        var ok = req.CreateResponse(HttpStatusCode.OK);
        await ok.WriteStringAsync("Product upserted.");
        return ok;
    }
}

/// <summary>
/// Product DTO used over HTTP; RowKey acts as the unique id/SKU.
/// </summary>
public record ProductDto(string RowKey, string Name, string? Description, decimal Price, string? ImageUrl);
