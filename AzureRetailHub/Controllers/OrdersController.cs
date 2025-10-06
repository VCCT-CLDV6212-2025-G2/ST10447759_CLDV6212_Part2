/*
 * Jeron Okkers
 * ST10447759
 * CLDV6212
 */
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using AzureRetailHub.Models;
using AzureRetailHub.Services;   // TableStorageService + FunctionApiClient
using AzureRetailHub.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AzureRetailHub.Controllers
{
    public class OrdersController : Controller
    {
        private readonly TableStorageService _table;
        private readonly StorageOptions _opts;
        private readonly FunctionApiClient _fx; // <-- NEW: we call Functions HTTP endpoints

        public OrdersController(
            TableStorageService table,
            IOptions<StorageOptions> options,
            FunctionApiClient fx)               // <-- inject FunctionApiClient (remove QueueStorageService)
        {
            _table = table;
            _opts = options.Value;
            _fx = fx;
        }

        // READS can still come straight from Table Storage
        public async Task<IActionResult> Index()
        {
            var list = new List<OrderDto>();
            await foreach (var e in _table.QueryEntitiesAsync(_opts.OrdersTable))
            {
                list.Add(new OrderDto
                {
                    RowKey = e.RowKey,
                    CustomerId = e.GetString("CustomerId") ?? "",
                    OrderDate = e.GetDateTime("OrderDate") ?? DateTime.UtcNow,
                    ItemsJson = e.GetString("ItemsJson"),
                    Status = e.GetString("Status")
                });
            }
            return View(list);
        }

        public async Task<IActionResult> Create()
        {
            var customers = new List<CustomerDto>();
            await foreach (var e in _table.QueryEntitiesAsync(_opts.CustomersTable))
            {
                customers.Add(new CustomerDto { RowKey = e.RowKey, FullName = e.GetString("FullName") });
            }

            var products = new List<ProductDto>();
            await foreach (var e in _table.QueryEntitiesAsync(_opts.ProductsTable))
            {
                products.Add(new ProductDto
                {
                    RowKey = e.RowKey,
                    Name = e.GetString("Name"),
                    Price = Convert.ToDecimal(e.GetDouble("Price"))
                });
            }

            var vm = new CreateOrderViewModel
            {
                AvailableCustomers = customers,
                AvailableProducts = products
            };
            return View(vm);
        }

        // WRITE: enqueue (do NOT write Orders table directly)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string customerId, string itemsJson)
        {
            if (string.IsNullOrWhiteSpace(customerId) || string.IsNullOrWhiteSpace(itemsJson))
            {
                ModelState.AddModelError("", "Customer and items are required.");
                return await Create(); // re-render with dropdowns
            }

            var orderId = Guid.NewGuid().ToString("N");
            var status = "Processing";
            var orderUtc = DateTime.UtcNow;

            // Send to Functions HTTP endpoint that enqueues the message
            var payload = new
            {
                Action = "CreateOrUpdate",
                OrderId = orderId,
                CustomerId = customerId,
                Status = status,
                OrderDate = orderUtc,
                ItemsJson = itemsJson
                // If your queue processor expects more fields (e.g., TotalAmount), include them here.
            };

            var res = await _fx.PostJsonAsync("orders/enqueue", payload);
            if (res is null)
            {
                ModelState.AddModelError("", "Failed to enqueue order. Please try again.");
                return await Create();
            }

            return RedirectToAction(nameof(Index));
        }

        // READ DETAILS from table (fine)
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var orderEntity = await _table.GetEntityAsync(_opts.OrdersTable, "ORDER", id);
            if (orderEntity == null) return NotFound();

            var order = new OrderDto
            {
                RowKey = orderEntity.RowKey,
                CustomerId = orderEntity.GetString("CustomerId"),
                OrderDate = orderEntity.GetDateTime("OrderDate") ?? DateTime.UtcNow,
                ItemsJson = orderEntity.GetString("ItemsJson"),
                Status = orderEntity.GetString("Status")
            };

            var customerEntity = await _table.GetEntityAsync(_opts.CustomersTable, "CUSTOMER", order.CustomerId);
            var customer = customerEntity != null
                ? new CustomerDto
                {
                    RowKey = customerEntity.RowKey,
                    FullName = customerEntity.GetString("FullName"),
                    Email = customerEntity.GetString("Email"),
                    Phone = customerEntity.GetString("Phone")
                }
                : new CustomerDto { FullName = "Customer not found" };

            var viewModel = new OrderDetailViewModel
            {
                Order = order,
                Customer = customer
            };

            viewModel.ParseItems();

            foreach (var item in viewModel.Items)
            {
                var productEntity = await _table.GetEntityAsync(_opts.ProductsTable, "PRODUCT", item.ProductId);
                item.ProductName = productEntity?.GetString("Name") ?? "Product not found";
            }

            return View(viewModel);
        }

        // READ existing order to edit its Status (fine)
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var entity = await _table.GetEntityAsync(_opts.OrdersTable, "ORDER", id);
            if (entity == null) return NotFound();

            var order = new OrderDto
            {
                RowKey = entity.RowKey,
                CustomerId = entity.GetString("CustomerId"),
                OrderDate = entity.GetDateTime("OrderDate") ?? DateTime.UtcNow,
                Status = entity.GetString("Status")
            };

            return View(order);
        }

        // WRITE: enqueue update (do NOT update Orders table directly)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, OrderDto order)
        {
            if (id != order.RowKey) return BadRequest();
            //if (!ModelState.IsValid) return View(order);

            // We fetch current entity to preserve fields like ItemsJson/OrderDate
            var current = await _table.GetEntityAsync(_opts.OrdersTable, "ORDER", id);
            //if (current == null) return NotFound();

            //var payload = new
            //{
            //    Action = "CreateOrUpdate",
            //    OrderId = id,
            //    CustomerId = current.GetString("CustomerId") ?? order.CustomerId ?? "",
            //    Status = string.IsNullOrWhiteSpace(order.Status) ? (current.GetString("Status") ?? "Pending") : order.Status,
            //    OrderDate = current.GetDateTime("OrderDate") ?? DateTime.UtcNow,
            //    ItemsJson = current.GetString("ItemsJson") ?? "[]"
            //};

            var partial = new TableEntity("ORDER", id)
            {
                ["Status"] = string.IsNullOrWhiteSpace(order.Status) ? "Pending" : order.Status
            };

            //var res = await _fx.PostJsonAsync("orders/enqueue", payload);
            //if (res is null)
            //{
            //    ModelState.AddModelError("", "Failed to enqueue order update. Please try again.");
            //    return View(order);
            //}
            await _table.UpdateEntityAsync(_opts.OrdersTable, partial, TableUpdateMode.Merge);


            return RedirectToAction(nameof(Index));
        }
    }
}
