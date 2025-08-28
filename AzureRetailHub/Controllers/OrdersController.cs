using Azure;
using Azure.Data.Tables;
using AzureRetailHub.Models;
using AzureRetailHub.Services;
using AzureRetailHub.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace AzureRetailHub.Controllers
{
    public class OrdersController : Controller
    {
        private readonly TableStorageService _table;
        private readonly QueueStorageService _queue;
        private readonly StorageOptions _opts;

        public OrdersController(TableStorageService table, QueueStorageService queue, IOptions<StorageOptions> options)
        {
            _table = table;
            _queue = queue;
            _opts = options.Value;
        }

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
                products.Add(new ProductDto { RowKey = e.RowKey, Name = e.GetString("Name"), Price = Convert.ToDecimal(e.GetDouble("Price")) });
            }

            var viewModel = new CreateOrderViewModel
            {
                AvailableCustomers = customers,
                AvailableProducts = products
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string customerId, string itemsJson)
        {
            if (string.IsNullOrEmpty(customerId) || string.IsNullOrEmpty(itemsJson))
            {
                return BadRequest("Customer and items are required.");
            }

            var order = new OrderDto
            {
                CustomerId = customerId,
                ItemsJson = itemsJson,
                Status = "Processing"
            };

            var entity = new TableEntity("ORDER", order.RowKey)
            {
                {"CustomerId", order.CustomerId},
                {"OrderDate", order.OrderDate},
                {"ItemsJson", order.ItemsJson ?? "[]"},
                {"Status", order.Status ?? "New"}
            };

            await _table.AddEntityAsync(_opts.OrdersTable, entity);

            var message = new { OrderId = order.RowKey, CustomerId = order.CustomerId, Action = "NewOrder" };
            await _queue.SendMessageAsync(_opts.QueueName, message);

            return RedirectToAction(nameof(Index));
        }

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

        // CORRECTED: This version ensures all data is preserved during an update.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, OrderDto order)
        {
            if (id != order.RowKey) return BadRequest();

            // Fetch the existing entity from the database
            var entity = await _table.GetEntityAsync(_opts.OrdersTable, "ORDER", id);
            if (entity == null)
            {
                return NotFound();
            }

            // Update only the status field from the submitted form
            entity["Status"] = order.Status;

            // Save the updated entity
            await _table.UpdateEntityAsync(_opts.OrdersTable, entity);

            return RedirectToAction(nameof(Index));
        }
    }
}