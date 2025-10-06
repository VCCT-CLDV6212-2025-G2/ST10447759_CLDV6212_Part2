/*
 * FILE: TableStorageService.cs
 * PROJECT: AzureRetailHub
 * PROGRAMMER: Jeron Okkers ST10447759
 * DESCRIPTION:
 * Centralized Table Storage helper for CRUD with sane defaults:
 *  - Update defaults to MERGE (partial updates)
 *  - Upsert supported (MERGE by default)
 *  - GetEntityAsync returns null on 404 (instead of throwing)
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

        public TableStorageService(IOptions<StorageOptions> options)
        {
            _opts = options.Value;
            _tableServiceClient = new TableServiceClient(_opts.ConnectionString);
        }

        /// Get a client for a specific table (creates if missing).
        public async Task<TableClient> GetTableClientAsync(string tableName)
        {
            var client = _tableServiceClient.GetTableClient(tableName);
            await client.CreateIfNotExistsAsync();
            return client;
        }

        /// Add a new entity.
        public async Task AddEntityAsync(string tableName, TableEntity entity)
        {
            var table = await GetTableClientAsync(tableName);
            await table.AddEntityAsync(entity);
        }

        /// Query entities with optional OData filter.
        public async IAsyncEnumerable<TableEntity> QueryEntitiesAsync(string tableName, string? filter = null)
        {
            var table = await GetTableClientAsync(tableName);
            await foreach (var e in table.QueryAsync<TableEntity>(filter))
            {
                yield return e;
            }
        }

        /// Get a single entity; returns null if not found.
        public async Task<TableEntity?> GetEntityAsync(string tableName, string partitionKey, string rowKey)
        {
            var table = await GetTableClientAsync(tableName);
            try
            {
                var resp = await table.GetEntityAsync<TableEntity>(partitionKey, rowKey);
                return resp.Value;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return null;
            }
        }

        /// Update an entity. Defaults to MERGE (partial update). If no ETag is present, uses ETag.All.
        public async Task UpdateEntityAsync(
            string tableName,
            TableEntity entity,
            TableUpdateMode mode = TableUpdateMode.Merge)
        {
            var table = await GetTableClientAsync(tableName);

            // If caller didn't supply an ETag (common for partial entities), use ETag.All (unconditional).
            ETag etag = entity.ETag;
            if (etag == default) etag = ETag.All;

            await table.UpdateEntityAsync(entity, etag, mode);
        }

        /// Upsert an entity (insert or update). Defaults to MERGE semantics.
        public async Task UpsertEntityAsync(
            string tableName,
            TableEntity entity,
            TableUpdateMode mode = TableUpdateMode.Merge)
        {
            var table = await GetTableClientAsync(tableName);
            await table.UpsertEntityAsync(entity, mode);
        }

        /// Delete an entity.
        public async Task DeleteEntityAsync(string tableName, string partitionKey, string rowKey)
        {
            var table = await GetTableClientAsync(tableName);
            await table.DeleteEntityAsync(partitionKey, rowKey);
        }
    }
}
