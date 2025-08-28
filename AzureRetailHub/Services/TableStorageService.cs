/*
 * FILE: TableStorageService.cs
 * PROJECT: AzureRetailHub
 * PROGRAMMER: Jeron Okkers ST10447759
 * DESCRIPTION:
 * This service class encapsulates all interactions with Azure Table Storage.
 * It provides a centralized way to manage table clients and perform CRUD 
 * (Create, Read, Update, Delete) operations on table entities.
 * REFERENCE: Microsoft Docs, "Get started with Azure Table Storage"
 * https://learn.microsoft.com/en-us/azure/storage/tables/storage-tables-dotnet-get-started
 */
using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Options;
using AzureRetailHub.Settings;

namespace AzureRetailHub.Services
{
    public class TableStorageService
    {
        private readonly TableServiceClient _tableServiceClient;
        private readonly StorageOptions _opts;

        // The constructor uses Dependency Injection to get the connection string from appsettings.json.
        public TableStorageService(IOptions<StorageOptions> options)
        {
            _opts = options.Value;
            // The TableServiceClient is the main entry point for interacting with the Table Storage service.
            _tableServiceClient = new TableServiceClient(_opts.ConnectionString);
        }

        /// Gets a client for a specific table, creating the table if it doesn't exist.
        public async Task<TableClient> GetTableClientAsync(string tableName)
        {
            var client = _tableServiceClient.GetTableClient(tableName);
            await client.CreateIfNotExistsAsync();
            return client;
        }

        /// Adds a new entity to a specified table.
        public async Task AddEntityAsync(string tableName, TableEntity entity)
        {
            var table = await GetTableClientAsync(tableName);
            await table.AddEntityAsync(entity);
        }

        /// Queries for entities in a table, optionally applying a filter.
        /// </summary>
        /// <param name="tableName">The name of the table.</param>
        /// <param name="filter">An OData filter string (optional).</param>
        /// <returns>An asynchronous stream of TableEntity objects.</returns>
        public async IAsyncEnumerable<TableEntity> QueryEntitiesAsync(string tableName, string? filter = null)
        {
            var table = await GetTableClientAsync(tableName);
            await foreach (var e in table.QueryAsync<TableEntity>(filter))
            {
                yield return e;
            }
        }

        /// Retrieves an entity from a table using its partition and row key.

        public async Task<TableEntity?> GetEntityAsync(string tableName, string partitionKey, string rowKey)
        {
            // NOTE: This method is used to get all items from a table (like all products or all customers).
            // It uses IAsyncEnumerable for efficiency, processing each entity one by one instead of loading them all into memory at once.
            var table = await GetTableClientAsync(tableName);
            var response = await table.GetEntityAsync<TableEntity>(partitionKey, rowKey);
            return response.Value;
        }

        /// Updates an existing entity in a table.
        public async Task UpdateEntityAsync(string tableName, TableEntity entity)
        {
            var table = await GetTableClientAsync(tableName);
            await table.UpdateEntityAsync(entity, ETag.All, TableUpdateMode.Replace);
        }

        /// Deletes an entity from a table using its partition and row key.
        public async Task DeleteEntityAsync(string tableName, string partitionKey, string rowKey)
        {
            var table = await GetTableClientAsync(tableName);
            await table.DeleteEntityAsync(partitionKey, rowKey);
        }
    }
}