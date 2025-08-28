/*
 * FILE: ProductsController.cs
 * DESCRIPTION:
 * This controller manages all actions related to Products. It handles creating,
 * viewing, editing, and deleting products. It integrates with both the
 * TableStorageService (for metadata) and the BlobStorageService (for images).
 */

using Azure;
using Azure.Data.Tables;
using AzureRetailHub.Models;
using AzureRetailHub.Services;
using AzureRetailHub.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace AzureRetailHub.Controllers
{
    public class ProductsController : Controller
    {
        // NOTE: Point out that this controller depends on two services, demonstrating dependency injection.
        private readonly TableStorageService _table;
        private readonly BlobStorageService _blob;
        private readonly StorageOptions _opts;

        public ProductsController(TableStorageService table, BlobStorageService blob, IOptions<StorageOptions> options)
        {
            _table = table;
            _blob = blob;
            _opts = options.Value;
        }

        public async Task<IActionResult> Index()
        {
            var list = new List<ProductDto>();
            //  NOTE: This loop reads all product entities from Azure Table Storage and converts them
            // into a list of 'ProductDto' models that can be used by the View.
            await foreach (var e in _table.QueryEntitiesAsync(_opts.ProductsTable))
            {
                string name = e.GetString("Name") ?? "";
                string desc = e.GetString("Description") ?? "";
                double priceDouble = 0;
                try { priceDouble = e.GetDouble("Price") ?? 0; } catch { }
                string imageUrl = e.GetString("ImageUrl") ?? "";

                var p = new ProductDto
                {
                    RowKey = e.RowKey,
                    Name = name,
                    Description = desc,
                    Price = Convert.ToDecimal(priceDouble),
                    ImageUrl = imageUrl
                };
                list.Add(p);
            }
            return View(list);
        }

        public IActionResult Create() => View(new ProductDto());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProductDto product, IFormFile? imageFile)
        {
            if (!ModelState.IsValid) return View(product);
            // VIDEO NOTE: This is where Blob Storage is used. If an image file is provided...
            if (imageFile != null && imageFile.Length > 0)
            {
                var ext = Path.GetExtension(imageFile.FileName);
                // Create a unique name for the blob to avoid naming conflicts.
                var blobName = $"{Guid.NewGuid()}{ext}";
                using var ms = new MemoryStream();
                await imageFile.CopyToAsync(ms);
                // ...call the Blob service to upload it and get the public URL.
                product.ImageUrl = await _blob.UploadFileAsync(_opts.BlobContainer, blobName, ms);
            }

            // A TableEntity is a simple dictionary of key-value pairs.
            var entity = new TableEntity("PRODUCT", product.RowKey)
            {
                {"Name", product.Name},
                {"Description", product.Description ?? "" },
                {"Price", Convert.ToDouble(product.Price)},
                {"ImageUrl", product.ImageUrl ?? "" }
            };

            await _table.AddEntityAsync(_opts.ProductsTable, entity);

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();
            var entity = await _table.GetEntityAsync(_opts.ProductsTable, "PRODUCT", id);
            if (entity == null) return NotFound();

            var product = new ProductDto
            {
                RowKey = entity.RowKey,
                Name = entity.GetString("Name") ?? "",
                Description = entity.GetString("Description") ?? "",
                Price = Convert.ToDecimal(entity.GetDouble("Price") ?? 0),
                ImageUrl = entity.GetString("ImageUrl") ?? ""
            };

            return View(product);
        }

        // ADDED: GET method for Edit
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();
            var entity = await _table.GetEntityAsync(_opts.ProductsTable, "PRODUCT", id);
            if (entity == null) return NotFound();

            var product = new ProductDto
            {
                RowKey = entity.RowKey,
                Name = entity.GetString("Name") ?? "",
                Description = entity.GetString("Description") ?? "",
                Price = Convert.ToDecimal(entity.GetDouble("Price") ?? 0),
                ImageUrl = entity.GetString("ImageUrl") ?? ""
            };
            return View(product);
        }

        // ADDED: POST method for Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, ProductDto product, IFormFile? imageFile)
        {
            if (id != product.RowKey) return BadRequest();
            if (!ModelState.IsValid) return View(product);

            var entity = await _table.GetEntityAsync(_opts.ProductsTable, "PRODUCT", id);
            if (entity == null) return NotFound();

            // Handle image update
            if (imageFile != null && imageFile.Length > 0)
            {
                // Delete the old image before uploading the new one
                var oldImageUrl = entity.GetString("ImageUrl");
                await _blob.DeleteFileAsync(_opts.BlobContainer, oldImageUrl);

                // Upload new image
                var ext = Path.GetExtension(imageFile.FileName);
                var blobName = $"{Guid.NewGuid()}{ext}";
                using var ms = new MemoryStream();
                await imageFile.CopyToAsync(ms);
                product.ImageUrl = await _blob.UploadFileAsync(_opts.BlobContainer, blobName, ms);
            }
            else
            {
                // Keep the old image if no new one is uploaded
                product.ImageUrl = entity.GetString("ImageUrl");
            }

            entity["Name"] = product.Name;
            entity["Description"] = product.Description ?? "";
            entity["Price"] = Convert.ToDouble(product.Price);
            entity["ImageUrl"] = product.ImageUrl ?? "";

            await _table.UpdateEntityAsync(_opts.ProductsTable, entity);
            return RedirectToAction(nameof(Index));
        }

        // ADDED: GET method for Delete
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();
            var entity = await _table.GetEntityAsync(_opts.ProductsTable, "PRODUCT", id);
            if (entity == null) return NotFound();

            var product = new ProductDto
            {
                RowKey = entity.RowKey,
                Name = entity.GetString("Name") ?? "",
                Description = entity.GetString("Description") ?? "",
                Price = Convert.ToDecimal(entity.GetDouble("Price") ?? 0),
                ImageUrl = entity.GetString("ImageUrl") ?? ""
            };
            return View(product);
        }

        // ADDED: POST method for Delete
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var entity = await _table.GetEntityAsync(_opts.ProductsTable, "PRODUCT", id);
            if (entity != null)
            {
                // Delete the associated image from blob storage
                var imageUrl = entity.GetString("ImageUrl");
                await _blob.DeleteFileAsync(_opts.BlobContainer, imageUrl);

                // Delete the entity from table storage
                await _table.DeleteEntityAsync(_opts.ProductsTable, "PRODUCT", id);
            }
            return RedirectToAction(nameof(Index));
        }
    }
}