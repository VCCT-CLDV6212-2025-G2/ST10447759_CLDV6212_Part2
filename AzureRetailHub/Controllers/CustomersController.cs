/*
 * Jeron Okkers
 * ST10447759
 * PROG6221
 */
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using AzureRetailHub.Models;
using AzureRetailHub.Services;   // <-- FunctionApiClient, TableStorageService
using AzureRetailHub.Settings;   // <-- StorageOptions
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AzureRetailHub.Controllers
{
    public class CustomersController : Controller
    {
        private readonly TableStorageService _table;
        private readonly StorageOptions _opts;
        private readonly FunctionApiClient _fx;   // <-- added

        public CustomersController(
            TableStorageService table,
            IOptions<StorageOptions> options,
            FunctionApiClient fx)                 // <-- added
        {
            _table = table;
            _opts = options.Value;
            _fx = fx;
        }

        // GET: Customers
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

        // GET: Customers/Create
        public IActionResult Create() => View();

        // POST: Customers/Create  (WRITE via Azure Function)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CustomerDto customer)
        {
            if (!ModelState.IsValid) return View(customer);

            // Ensure an ID (RowKey) exists
            var id = string.IsNullOrWhiteSpace(customer.RowKey)
                ? Guid.NewGuid().ToString("N")
                : customer.RowKey;

            var payload = new CustomerUpsertDto(
                RowKey: id,
                FullName: customer.FullName ?? string.Empty,
                Email: customer.Email,
                Phone: customer.Phone
            );

            var result = await _fx.PostJsonAsync("customers", payload);
            if (result is null)
            {
                ModelState.AddModelError("", "Failed to create customer via Functions API.");
                return View(customer);
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Customers/Details/{id} (READ direct from Table Storage is fine)
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

        // POST: Customers/Edit/{id}  (WRITE via Azure Function)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, CustomerDto customer)
        {
            if (id != customer.RowKey) return BadRequest();
            if (!ModelState.IsValid) return View(customer);

            var payload = new CustomerUpsertDto(
                RowKey: customer.RowKey,
                FullName: customer.FullName ?? string.Empty,
                Email: customer.Email,
                Phone: customer.Phone
            );

            var result = await _fx.PostJsonAsync("customers", payload);
            if (result is null)
            {
                ModelState.AddModelError("", "Failed to update customer via Functions API.");
                return View(customer);
            }

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

        // POST: Customers/Delete/{id}  (WRITE via Azure Function)
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest();

            var ok = await _fx.DeleteAsync($"customers/{id}");
            if (!ok)
            {
                // Show a gentle error and return to details if delete failed
                ModelState.AddModelError("", "Failed to delete customer via Functions API.");
                return RedirectToAction(nameof(Details), new { id });
            }

            return RedirectToAction(nameof(Index));
        }

        // ----------------- helper payload -----------------
        // Payload sent to the Customers HTTP Function
        private record CustomerUpsertDto(string RowKey, string FullName, string? Email, string? Phone);
    }
}
