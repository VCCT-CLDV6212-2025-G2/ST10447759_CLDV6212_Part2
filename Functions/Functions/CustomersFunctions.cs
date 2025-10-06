using System.Net;
using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;

namespace AzureRetailHub.Functions;

public class CustomersFunctions
{
    private readonly IConfiguration _config;
    public CustomersFunctions(IConfiguration config) => _config = config;

    [Function("CustomerUpsert")]
    public async Task<HttpResponseData> UpsertAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", "put", Route = "customers")] HttpRequestData req)
    {
        var body = await new StreamReader(req.Body).ReadToEndAsync();
        var dto = JsonSerializer.Deserialize<CustomerDto>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (dto is null || string.IsNullOrWhiteSpace(dto.RowKey) || string.IsNullOrWhiteSpace(dto.FullName))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Invalid customer payload");
            return bad;
        }

        var cs = _config["StorageOptions:ConnectionString"];
        var tableName = _config["StorageOptions:CustomersTable"];
        var table = new TableClient(cs, tableName);
        await table.CreateIfNotExistsAsync();

        var entity = new TableEntity("CUSTOMER", dto.RowKey)
        {
            ["FullName"] = dto.FullName,
            ["Email"] = dto.Email ?? "",
            ["Phone"] = dto.Phone ?? ""
        };

        await table.UpsertEntityAsync(entity, TableUpdateMode.Replace);

        var ok = req.CreateResponse(HttpStatusCode.OK);
        await ok.WriteStringAsync("Customer upserted.");
        return ok;
    }

    [Function("CustomerDelete")]
    public async Task<HttpResponseData> DeleteAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "customers/{id}")] HttpRequestData req, string id)
    {
        var cs = _config["StorageOptions:ConnectionString"];
        var tableName = _config["StorageOptions:CustomersTable"];
        var table = new TableClient(cs, tableName);
        await table.CreateIfNotExistsAsync();

        await table.DeleteEntityAsync("CUSTOMER", id);

        var ok = req.CreateResponse(HttpStatusCode.OK);
        await ok.WriteStringAsync("Customer deleted.");
        return ok;
    }
}

// Minimal DTO (same shape as your MVC model)
public record CustomerDto(string RowKey, string FullName, string? Email, string? Phone);
