using Azure.Data.Tables;
using AzureRetailHub.Models;
using AzureRetailHub.Services;
using AzureRetailHub.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AzureRetailHub.Controllers
{
    public class CustomersController : Controller
    {
        private readonly TableStorageService _table;
        private readonly StorageOptions _opts;

        public CustomersController(TableStorageService table, IOptions<StorageOptions> options)
        {
            _table = table;
            _opts = options.Value;
        }

        public async Task<IActionResult> Index()
        {
            var list = new List<CustomerDto>();
            await foreach (var e in _table.QueryEntitiesAsync(_opts.CustomersTable))
            {
                list.Add(new CustomerDto
                {
                    RowKey = e.RowKey,
                    FullName = e.GetString("FullName") ?? "",
                    Email = e.GetString("Email"),
                    Phone = e.GetString("Phone")
                });
            }
            return View(list);
        }

        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CustomerDto customer)
        {
            if (!ModelState.IsValid) return View(customer);

            var entity = new TableEntity("CUSTOMER", customer.RowKey)
            {
                {"FullName", customer.FullName},
                {"Email", customer.Email ?? ""},
                {"Phone", customer.Phone ?? ""}
            };

            await _table.AddEntityAsync(_opts.CustomersTable, entity);
            return RedirectToAction(nameof(Index));
        }

        // GET: Customers/Details/{id}
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();
            var entity = await _table.GetEntityAsync(_opts.CustomersTable, "CUSTOMER", id);
            if (entity == null) return NotFound();

            var customer = new CustomerDto
            {
                RowKey = entity.RowKey,
                FullName = entity.GetString("FullName") ?? "",
                Email = entity.GetString("Email"),
                Phone = entity.GetString("Phone")
            };
            return View(customer);
        }

        // GET: Customers/Edit/{id}
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();
            var entity = await _table.GetEntityAsync(_opts.CustomersTable, "CUSTOMER", id);
            if (entity == null) return NotFound();

            var customer = new CustomerDto
            {
                RowKey = entity.RowKey,
                FullName = entity.GetString("FullName") ?? "",
                Email = entity.GetString("Email"),
                Phone = entity.GetString("Phone")
            };
            return View(customer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, CustomerDto customer)
        {
            if (id != customer.RowKey) return BadRequest();
            if (!ModelState.IsValid) return View(customer);

            var entity = new TableEntity("CUSTOMER", customer.RowKey)
            {
                {"FullName", customer.FullName},
                {"Email", customer.Email ?? ""},
                {"Phone", customer.Phone ?? ""}
            };

            await _table.UpdateEntityAsync(_opts.CustomersTable, entity);
            return RedirectToAction(nameof(Index));
        }

        // GET: Customers/Delete/{id}
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();
            var entity = await _table.GetEntityAsync(_opts.CustomersTable, "CUSTOMER", id);
            if (entity == null) return NotFound();

            var customer = new CustomerDto
            {
                RowKey = entity.RowKey,
                FullName = entity.GetString("FullName") ?? "",
                Email = entity.GetString("Email"),
                Phone = entity.GetString("Phone")
            };
            return View(customer);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            await _table.DeleteEntityAsync(_opts.CustomersTable, "CUSTOMER", id);
            return RedirectToAction(nameof(Index));
        }
    }
}