using AzureRetailHub.Models;
using AzureRetailHub.Services;
using AzureRetailHub.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace AzureRetailHub.Controllers
{
    public class ContractsController : Controller
    {
        private readonly FileStorageService _files;
        private readonly StorageOptions _opts;

        public ContractsController(FileStorageService files, IOptions<StorageOptions> options)
        {
            _files = files;
            _opts = options.Value;
        }

        public async Task<IActionResult> Index()
        {
            var fileList = new List<FileDetailDto>();
            await foreach (var item in _files.ListFilesAsync(_opts.FileShareName))
            {
                var fileClient = await _files.GetFileClientAsync(_opts.FileShareName, item.Name);
                var properties = await fileClient.GetPropertiesAsync();
                fileList.Add(new FileDetailDto
                {
                    Name = item.Name,
                    Size = properties.Value.ContentLength,
                    UploadedOn = properties.Value.LastModified
                });
            }
            return View(fileList);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(IFormFile contractFile)
        {
            if (contractFile == null || contractFile.Length == 0)
            {
                TempData["ErrorMessage"] = "Please choose a file to upload.";
                return RedirectToAction(nameof(Index));
            }

            var fileName = Path.GetFileName(contractFile.FileName);
            using var ms = new MemoryStream();
            await contractFile.CopyToAsync(ms);
            await _files.UploadFileAsync(_opts.FileShareName, fileName, ms);

            TempData["SuccessMessage"] = "File uploaded successfully!";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Download(string fileName)
        {
            var stream = await _files.GetFileStreamAsync(_opts.FileShareName, fileName);
            if (stream == null)
            {
                return NotFound();
            }
            return File(stream, "application/octet-stream", fileName);
        }

        public async Task<IActionResult> Delete(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return NotFound();

            var fileClient = await _files.GetFileClientAsync(_opts.FileShareName, fileName);
            if (!await fileClient.ExistsAsync()) return NotFound();

            var properties = await fileClient.GetPropertiesAsync();
            var model = new FileDetailDto
            {
                Name = fileName,
                Size = properties.Value.ContentLength,
                UploadedOn = properties.Value.LastModified
            };
            return View(model);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string fileName)
        {
            await _files.DeleteFileAsync(_opts.FileShareName, fileName);
            TempData["SuccessMessage"] = "File deleted successfully!";
            return RedirectToAction(nameof(Index));
        }
    }
}