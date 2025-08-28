using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;
using Microsoft.Extensions.Options;
using AzureRetailHub.Settings;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace AzureRetailHub.Services
{
    public class FileStorageService
    {
        private readonly ShareServiceClient _shareServiceClient;
        private readonly StorageOptions _opts;

        public FileStorageService(IOptions<StorageOptions> opts)
        {
            _opts = opts.Value;
            _shareServiceClient = new ShareServiceClient(_opts.ConnectionString);
        }

        private async Task<ShareDirectoryClient> GetRootDirectoryClient(string shareName)
        {
            var shareClient = _shareServiceClient.GetShareClient(shareName);
            await shareClient.CreateIfNotExistsAsync();
            return shareClient.GetRootDirectoryClient();
        }

        // Helper method to get a file client
        public async Task<ShareFileClient> GetFileClientAsync(string shareName, string fileName)
        {
            var root = await GetRootDirectoryClient(shareName);
            return root.GetFileClient(fileName);
        }

        public async Task UploadFileAsync(string shareName, string fileName, Stream data)
        {
            var fileClient = await GetFileClientAsync(shareName, fileName);
            data.Position = 0;
            await fileClient.CreateAsync(data.Length);
            await fileClient.UploadRangeAsync(new Azure.HttpRange(0, data.Length), data);
        }

        public async IAsyncEnumerable<ShareFileItem> ListFilesAsync(string shareName)
        {
            var root = await GetRootDirectoryClient(shareName);
            await foreach (var item in root.GetFilesAndDirectoriesAsync())
            {
                if (!item.IsDirectory)
                {
                    yield return item;
                }
            }
        }

        public async Task<Stream> GetFileStreamAsync(string shareName, string fileName)
        {
            var fileClient = await GetFileClientAsync(shareName, fileName);
            if (await fileClient.ExistsAsync())
            {
                var download = await fileClient.DownloadAsync();
                return download.Value.Content;
            }
            return null;
        }

        public async Task DeleteFileAsync(string shareName, string fileName)
        {
            var fileClient = await GetFileClientAsync(shareName, fileName);
            await fileClient.DeleteIfExistsAsync();
        }
    }
}