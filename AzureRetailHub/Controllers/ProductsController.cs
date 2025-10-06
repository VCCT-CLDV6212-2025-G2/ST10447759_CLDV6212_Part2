/*
 * FILE: ProductsController.cs
 * DESCRIPTION:
 * This controller manages all actions related to Products. It handles creating,
 * viewing, editing, and deleting products. It integrates with both the
 * TableStorageService (for metadata) and the BlobStorageService (for images).
 */
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using AzureRetailHub.Models;      // ProductDto
using AzureRetailHub.Services;    // TableStorageService, FunctionApiClient
using AzureRetailHub.Settings;    // StorageOptions
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AzureRetailHub.Controllers
{
    public class ProductsController : Controller
    {
        private readonly TableStorageService _table;
        private readonly StorageOptions _opts;
        private readonly FunctionApiClient _fx;

        public ProductsController(
            TableStorageService table,
            IOptions<StorageOptions> options,
            FunctionApiClient fx)
        {
            _table = table;
            _opts = options.Value;
            _fx = fx;
        }

        // GET: Products (with simple search on Name)
        public async Task<IActionResult> Index(string? q)
        {
            var list = new List<ProductDto>();
            await foreach (var e in _table.QueryEntitiesAsync(_opts.ProductsTable, "PartitionKey eq 'PRODUCT'"))
            {
                var dto = MapToVm(e);
                if (string.IsNullOrWhiteSpace(q) ||
                    (dto.Name ?? string.Empty).Contains(q, StringComparison.OrdinalIgnoreCase))
                {
                    list.Add(dto);
                }
            }
            ViewBag.Query = q;
            return View(list);
        }

        // GET: Products/Details/{id}
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();
            try
            {
                var entity = await _table.GetEntityAsync(_opts.ProductsTable, "PRODUCT", id);
                if (entity == null) return NotFound();
                return View(MapToVm(entity));
            }
            catch (RequestFailedException)
            {
                return NotFound();
            }
        }

        // GET: Products/Create
        public IActionResult Create() => View(new ProductDto());

        // POST: Products/Create (image upload via Function + upsert via Function)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProductDto model, IFormFile? imageFile)
        {
            if (!ModelState.IsValid) return View(model);

            var id = string.IsNullOrWhiteSpace(model.RowKey) ? Guid.NewGuid().ToString("N") : model.RowKey;

            string? imageUrl = model.ImageUrl;
            if (imageFile is not null && imageFile.Length > 0)
            {
                imageUrl = await _fx.PostFileAsync("products/image", imageFile);
                if (imageUrl is null)
                {
                    ModelState.AddModelError("", "Image upload failed via Functions API.");
                    return View(model);
                }
            }

            var payload = new ProductUpsertDto(
                RowKey: id,
                Name: model.Name ?? string.Empty,
                Description: model.Description,
                Price: model.Price,
                ImageUrl: imageUrl
            );

            var res = await _fx.PostJsonAsync("products", payload);
            if (res is null)
            {
                ModelState.AddModelError("", "Product create failed via Functions API.");
                return View(model);
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Products/Edit/{id}
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();
            var e = await _table.GetEntityAsync(_opts.ProductsTable, "PRODUCT", id);
            if (e is null) return NotFound();
            return View(MapToVm(e));
        }

        // POST: Products/Edit/{id} (image upload via Function + upsert via Function)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, ProductDto model, IFormFile? imageFile)
        {
            if (id != model.RowKey) return BadRequest();
            if (!ModelState.IsValid) return View(model);

            string? imageUrl = model.ImageUrl;
            if (imageFile is not null && imageFile.Length > 0)
            {
                imageUrl = await _fx.PostFileAsync("products/image", imageFile);
                if (imageUrl is null)
                {
                    ModelState.AddModelError("", "Image upload failed via Functions API.");
                    return View(model);
                }
            }

            var payload = new ProductUpsertDto(
                RowKey: model.RowKey!,
                Name: model.Name ?? string.Empty,
                Description: model.Description,
                Price: model.Price,
                ImageUrl: imageUrl
            );

            var res = await _fx.PostJsonAsync("products", payload);
            if (res is null)
            {
                ModelState.AddModelError("", "Product update failed via Functions API.");
                return View(model);
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Products/Delete/{id}
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();
            var e = await _table.GetEntityAsync(_opts.ProductsTable, "PRODUCT", id);
            if (e is null) return NotFound();
            return View(MapToVm(e));
        }

        // NOTE: If you want to support delete, you can either:
        // 1) Add a Products Delete function, OR
        // 2) Keep deletes as direct table writes (brief only mandates Orders to be queue-updated).
        // Below we keep a direct delete for Products for simplicity. If you implemented a Function,
        // swap this to _fx.DeleteAsync("products/{id}") accordingly.

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            await _table.DeleteEntityAsync(_opts.ProductsTable, "PRODUCT", id);
            return RedirectToAction(nameof(Index));
        }

        // ---------------- helpers ----------------

        private static ProductDto MapToVm(TableEntity e) => new ProductDto
        {
            RowKey = e.RowKey,
            Name = e.GetString("Name") ?? "",
            Description = e.GetString("Description"),
            Price = (decimal)(e.GetDouble("Price") ?? 0.0),
            ImageUrl = e.GetString("ImageUrl")
        };

        private record ProductUpsertDto(string RowKey, string Name, string? Description, decimal Price, string? ImageUrl);
    }
}
