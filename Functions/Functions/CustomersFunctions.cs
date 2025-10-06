using System.Net;
using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;

namespace AzureRetailHub.Functions;

/// <summary>
/// Minimal HTTP endpoints for creating/updating and deleting Customers
/// in Azure Table Storage (PartitionKey fixed to "CUSTOMER").
/// - Returns friendly 400s on bad payloads and 200/OK on success.
/// </summary>
public class CustomersFunctions
{
    private readonly IConfiguration _config;
    public CustomersFunctions(IConfiguration config) => _config = config;

    /// <summary>
    /// Upserts (create/replace) a Customer record in Table Storage.

    /// Returns:
    ///   200 OK - "Customer upserted."
    ///   400 BadRequest - if required fields are missing
    /// </summary>
    [Function("CustomerUpsert")]
    public async Task<HttpResponseData> UpsertAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", "put", Route = "customers")] HttpRequestData req)
    {
        // Read and deserialize request body (case-insensitive to be friendly to callers)
        var body = await new StreamReader(req.Body).ReadToEndAsync();
        var dto = JsonSerializer.Deserialize<CustomerDto>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        // Validate minimal business rules
        if (dto is null || string.IsNullOrWhiteSpace(dto.RowKey) || string.IsNullOrWhiteSpace(dto.FullName))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Invalid customer payload");
            return bad;
        }

        // Resolve storage configuration and ensure table exists
        var cs = _config["StorageOptions:ConnectionString"];
        var tableName = _config["StorageOptions:CustomersTable"];
        var table = new TableClient(cs, tableName);
        await table.CreateIfNotExistsAsync();

        // Map DTO → TableEntity (PartitionKey kept constant for simple partitioning/queries)
        var entity = new TableEntity("CUSTOMER", dto.RowKey)
        {
            ["FullName"] = dto.FullName,
            ["Email"] = dto.Email ?? "",
            ["Phone"] = dto.Phone ?? ""
        };

        // Upsert using Replace to ensure a clean overwrite
        await table.UpsertEntityAsync(entity, TableUpdateMode.Replace);

        var ok = req.CreateResponse(HttpStatusCode.OK);
        await ok.WriteStringAsync("Customer upserted.");
        return ok;
    }

    /// <summary>
    /// Deletes a Customer by RowKey.
    /// 
    /// Route: DELETE /api/customers/{id}
    /// Returns:
    ///   200 OK - always (Table service ignores missing entities on delete)
    /// </summary>
    [Function("CustomerDelete")]
    public async Task<HttpResponseData> DeleteAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "customers/{id}")] HttpRequestData req, string id)
    {
        var cs = _config["StorageOptions:ConnectionString"];
        var tableName = _config["StorageOptions:CustomersTable"];
        var table = new TableClient(cs, tableName);
        await table.CreateIfNotExistsAsync();

        // PartitionKey is fixed to "CUSTOMER", RowKey arrives as {id}
        await table.DeleteEntityAsync("CUSTOMER", id);

        var ok = req.CreateResponse(HttpStatusCode.OK);
        await ok.WriteStringAsync("Customer deleted.");
        return ok;
    }
}

/// <summary>
/// Minimal DTO for customer transport over HTTP.
/// (Same shape as MVC model; RowKey maps to Table Storage RowKey)
/// </summary>
public record CustomerDto(string RowKey, string FullName, string? Email, string? Phone);
