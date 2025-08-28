/*
 * FILE: BlobStorageService.cs
 * DESCRIPTION:
 * This service manages interactions with Azure Blob Storage. It is responsible for
 * uploading and deleting files (in this case, product images).
 * REFERENCE: Microsoft Docs, "Quickstart: Azure Blob Storage library - .NET"
 * https://learn.microsoft.com/en-us/azure/storage/blobs/storage-blobs-dotnet-get-started
 */
using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;
using AzureRetailHub.Settings;
using System.IO;
using System.Threading.Tasks;
using System;

namespace AzureRetailHub.Services
{
    public class BlobStorageService
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly StorageOptions _opts;

        public BlobStorageService(IOptions<StorageOptions> opts)
        {
            _opts = opts.Value;
            //NOTE: Like the Table Storage service, the constructor gets the connection string.
            // The BlobServiceClient is the main object for working with containers and blobs.
            _blobServiceClient = new BlobServiceClient(_opts.ConnectionString);
        }

        /// <summary>
        /// Uploads a file stream to a specified blob container.
        /// <param name="containerName">The name of the blob container.</param>
        /// <param name="blobName">The desired name for the blob (file).</param>
        /// <param name="stream">The file content as a stream.</param>
        /// <returns>The public URL of the uploaded blob.</returns>
        public async Task<string> UploadFileAsync(string containerName, string blobName, Stream stream)
        {
            // VIDEO NOTE: This is a critical method for the "Products" feature. Explain the steps:
            // 1. Get a client for the container (like a folder). Create it if it doesn't exist.
            // 2. Set the public access policy so that browsers can display the images.
            // 3. Get a client for the specific blob (the file itself).
            // 4. Upload the stream data.
            // 5. Return the public URL, which gets saved in the product's table record.
            var container = _blobServiceClient.GetBlobContainerClient(containerName);
            await container.CreateIfNotExistsAsync();
            await container.SetAccessPolicyAsync(Azure.Storage.Blobs.Models.PublicAccessType.Blob);
            var blobClient = container.GetBlobClient(blobName);
            stream.Position = 0;
            await blobClient.UploadAsync(stream, overwrite: true);
            return blobClient.Uri.ToString();
        }

        // ADDED: Method to delete a blob
        public async Task DeleteFileAsync(string containerName, string blobUrl)
        {
            if (string.IsNullOrEmpty(blobUrl)) return;

            var container = _blobServiceClient.GetBlobContainerClient(containerName);
            // Extract the blob name from the full URL
            var blobName = new Uri(blobUrl).Segments.Last();
            var blobClient = container.GetBlobClient(blobName);
            await blobClient.DeleteIfExistsAsync();
        }
    }
}