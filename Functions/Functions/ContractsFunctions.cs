/*
 * FILE: ContractsFunctions.cs
 * PROJECT: AzureRetailHub.Functions (Azure Functions, .NET Isolated)
 * AUTHOR: Jeron Okkers (ST10447759)
 *
 * PURPOSE (Demo talking points):
 *  - Accept multipart/form-data uploads from the MVC app.
 *  - Parse the HTTP request (boundary + sections) safely.
 *  - Save each uploaded file into Azure File Share (Azure Files).
 *  - Return the stored filenames as JSON.
 *
 * HOW THIS IMPROVES THE APP (link to Part 2 outcomes):
 *  - Offloads binary handling to a Function (scalable, decoupled).
 *  - Uses the most appropriate Azure storage service for contracts (file semantics).
 *
 * REFERENCE (single authoritative source):
 *  - Microsoft Learn — Azure Files with .NET (ShareClient / ShareFileClient usage):
 *    https://learn.microsoft.com/azure/storage/files/storage-files-how-to-use-dotnet
 */

using System.Net;
using Azure;                                  // HttpRange
using Azure.Storage.Files.Shares;             // ShareClient, ShareDirectoryClient, ShareFileClient
using Microsoft.AspNetCore.WebUtilities;      // MultipartReader, MultipartSection
using Microsoft.Net.Http.Headers;             // MediaTypeHeaderValue, HeaderUtilities, ContentDispositionHeaderValue
using Microsoft.Azure.Functions.Worker;       // Function attribute (Isolated worker)
using Microsoft.Azure.Functions.Worker.Http;  // HttpRequestData/HttpResponseData
using Microsoft.Extensions.Configuration;     // IConfiguration

namespace AzureRetailHub.Functions.Functions;

public class ContractsFunctions
{
    // IConfiguration lets us read values from local.settings.json / app settings in Azure
    private readonly IConfiguration _config;

    public ContractsFunctions(IConfiguration config) => _config = config;

    /// <summary>
    /// Receives a multipart/form-data POST and writes each file to Azure File Share.
    /// Route: POST /api/contracts
    ///
    /// Demo script cue:
    ///   1) Client posts multipart/form-data with one or more "file" parts.
    ///   2) We validate Content-Type + boundary.
    ///   3) We stream each file section into memory, then to Azure Files.
    ///   4) Respond with JSON listing the stored names.
    ///
    /// NOTE: Authorization is Anonymous for local/demo convenience.
    ///       Switch to AuthorizationLevel.Function for production to require a function key.
    /// </summary>
    [Function("ContractUpload")]
    public async Task<HttpResponseData> UploadAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "contracts")] HttpRequestData req)
    {
        // --- Step 1: Validate Content-Type header ---
        // We expect: Content-Type: multipart/form-data; boundary=---XYZ
        if (!req.Headers.TryGetValues("Content-Type", out var ctVals))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Content-Type header missing.");
            return bad;
        }

        var contentType = ctVals.FirstOrDefault();
        // Parse Content-Type and confirm it's multipart/form-data
        if (!MediaTypeHeaderValue.TryParse(contentType, out var mt) ||
            !string.Equals(mt?.MediaType.Value, "multipart/form-data", StringComparison.OrdinalIgnoreCase))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Content-Type must be multipart/form-data.");
            return bad;
        }

        // Every multipart payload must have a boundary we can use to split sections
        var boundary = HeaderUtilities.RemoveQuotes(mt!.Boundary).Value;
        if (string.IsNullOrWhiteSpace(boundary))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Missing multipart boundary.");
            return bad;
        }

        // --- Step 2: Prepare Azure Files clients ---
        // Read connection info from configuration (local.settings.json / Azure App Settings)
        var cs = _config["StorageOptions:ConnectionString"];
        var shareName = _config["StorageOptions:FileShareName"];

        // ShareClient is the main entry point to an Azure File Share
        var share = new ShareClient(cs, shareName);
        await share.CreateIfNotExistsAsync(); // Idempotent: creates the share on first use

        // We'll upload into the root directory. You can change to a subdirectory if needed.
        var root = share.GetRootDirectoryClient();

        // --- Step 3: Parse the multipart body ---
        // MultipartReader reads each section (either a regular form field or a file)
        var reader = new MultipartReader(boundary, req.Body);
        MultipartSection? section;
        var uploaded = new List<string>();

        // Optional guardrail (demo talking point): reject overly large uploads
        // to prevent memory pressure during demo environments.
        const long maxSingleFileBytes = 10 * 1024 * 1024; // 10 MB

        while ((section = await reader.ReadNextSectionAsync()) != null)
        {
            // Each section should contain a Content-Disposition that tells us if it's a file
            if (!ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var cd))
                continue;

            var isFile = cd.DispositionType.Equals("form-data")
                         && (cd.FileName.HasValue || cd.FileNameStar.HasValue);
            if (!isFile)
                continue;

            // Choose a safe final name (prefix with GUID to avoid collisions)
            var originalName = cd.FileName.Value ?? cd.FileNameStar.Value ?? "upload.bin";
            var finalName = $"{Guid.NewGuid()}_{originalName}";

            // Read the file content into memory to know the length for Azure Files CreateAsync
            // (ShareFileClient.CreateAsync requires the file length up-front)
            using var ms = new MemoryStream();
            await section.Body.CopyToAsync(ms);


            ms.Position = 0;

            // --- Step 4: Save to Azure File Share ---
            // Create the file with exact length, then upload the range (0..length)
            var file = root.GetFileClient(finalName);
            await file.CreateAsync(ms.Length);
            ms.Position = 0;
            await file.UploadRangeAsync(new HttpRange(0, ms.Length), ms);

            uploaded.Add(finalName);
        }

        // If we got here with no files, the request was malformed or empty
        if (uploaded.Count == 0)
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("No file part found.");
            return bad;
        }

        // --- Step 5: Return success with JSON payload listing stored names ---
        var ok = req.CreateResponse(HttpStatusCode.OK);
        await ok.WriteAsJsonAsync(new { files = uploaded });
        return ok;
    }
}
