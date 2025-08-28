using System.Net.Http;
using Azure.Data.Tables;
using AzureRetailHub.Models;
using AzureRetailHub.Services;
using AzureRetailHub.Settings;
using Microsoft.Extensions.Options;

namespace AzureRetailHub.Data
{
    public static class SeedData
    {
        // sample products with remote image URLs (picsum)
        private static readonly (string name, string desc, decimal price, string imageUrl)[] Samples =
        {
            ("Cozy Knit Sweater", "Soft warm sweater", 79.99m, "https://picsum.photos/seed/p1/800/600"),
            ("Leather Ankle Boots", "Stylish leather boots", 129.99m, "https://picsum.photos/seed/p2/800/600"),
            ("Classic Trench Coat", "Timeless outerwear", 149.99m, "https://picsum.photos/seed/p3/800/600"),
            ("Cashmere Scarf", "Luxurious scarf", 49.99m, "https://picsum.photos/seed/p4/800/600"),
            ("Slim Fit Jeans", "Comfort stretch denim", 59.99m, "https://picsum.photos/seed/p5/800/600")
        };

        public static async Task SeedProductsAsync(TableStorageService table, BlobStorageService blob, StorageOptions opts, bool force = false)
        {
            // check if table already has rows and skip unless force
            var any = false;
            await foreach (var e in table.QueryEntitiesAsync(opts.ProductsTable))
            {
                any = true;
                break;
            }
            if (any && !force) return;

            using var http = new HttpClient();

            for (int i = 0; i < Samples.Length; i++)
            {
                var s = Samples[i];
                // download image bytes
                var imgBytes = await http.GetByteArrayAsync(s.imageUrl);
                using var ms = new MemoryStream(imgBytes);

                var blobName = $"seed-{i}-{Guid.NewGuid()}.jpg";
                var blobUrl = await blob.UploadFileAsync(opts.BlobContainer, blobName, ms);

                var product = new ProductDto
                {
                    RowKey = Guid.NewGuid().ToString(),
                    Name = s.name,
                    Description = s.desc,
                    Price = s.price,
                    ImageUrl = blobUrl
                };

                var entity = new TableEntity("PRODUCT", product.RowKey)
                {
                    {"Name", product.Name},
                    {"Description", product.Description ?? ""},
                    {"Price", Convert.ToDouble(product.Price)},
                    {"ImageUrl", product.ImageUrl ?? ""}
                };

                await table.AddEntityAsync(opts.ProductsTable, entity);
            }
        }
    }
}
