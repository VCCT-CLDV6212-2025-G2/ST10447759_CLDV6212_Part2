/*
 * Jeron Okkers
 * ST10447759
 * PROG6221
 */ 
using AzureRetailHub.Models;
using AzureRetailHub.Services;
using AzureRetailHub.Settings;
using Azure.Data.Tables;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AzureRetailHub.Controllers
{
    public class HomeController : Controller
    {
        private readonly TableStorageService _table;
        private readonly StorageOptions _opts;

        public HomeController(TableStorageService table, IOptions<StorageOptions> options)
        {
            _table = table;
            _opts = options.Value;
        }

        public async Task<IActionResult> Index()
        {
            var products = new List<ProductDto>();

            await foreach (var e in _table.QueryEntitiesAsync(_opts.ProductsTable))
            {
                var p = new ProductDto
                {
                    RowKey = e.RowKey,
                    Name = e.GetString("Name") ?? "",
                    Description = e.GetString("Description") ?? "",
                    Price = Convert.ToDecimal(e.GetDouble("Price") ?? 0),
                    ImageUrl = e.GetString("ImageUrl") ?? ""
                };
                products.Add(p);
            }

            // Show latest 4 products as "New Arrivals", top 4 as "Best Sellers" (for demo)
            var viewModel = new HomeViewModel
            {
                NewArrivals = products.Take(4).ToList(),
                BestSellers = products.Skip(4).Take(4).ToList()
            };

            return View(viewModel);
        }
    }

    public class HomeViewModel
    {
        public List<ProductDto> NewArrivals { get; set; } = new();
        public List<ProductDto> BestSellers { get; set; } = new();
    }
}
//================================================================================================================================================================//
