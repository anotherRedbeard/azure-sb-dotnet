using System.Text.Json;
using System.Text.Json.Serialization; // Needed for JsonNumberHandling
using Azure;
using Azure.Data.Tables;
using Azure.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Company.Function;

public class TableStorageHttpTrigger
{
    private readonly ILogger<TableStorageHttpTrigger> _logger;

    public TableStorageHttpTrigger(ILogger<TableStorageHttpTrigger> logger)
    {
        _logger = logger;
    }

    [Function("TableStorageHttpTrigger")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", "delete")] HttpRequest req)
    {
        var action = (req.Query["action"].FirstOrDefault() ?? "").Trim();
        if (string.IsNullOrEmpty(action))
            return Bad("Missing action. Supported: createTable, deleteTable, upsertEntity, getEntity, deleteEntity, query");

        // Gather environment
        var raw = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
        var tableServiceUri = Environment.GetEnvironmentVariable("AzureWebJobsStorage__tableServiceUri");
        var accountName = Environment.GetEnvironmentVariable("AzureWebJobsStorage__accountName");

        try
        {
            TableServiceClient serviceClient;
            string mode;

            bool LooksLikeConnString(string v) =>
                v.Equals("UseDevelopmentStorage=true", StringComparison.OrdinalIgnoreCase) ||
                v.Contains("AccountName=", StringComparison.OrdinalIgnoreCase) ||
                v.Contains("DefaultEndpointsProtocol=", StringComparison.OrdinalIgnoreCase) ||
                v.Contains("TableEndpoint=", StringComparison.OrdinalIgnoreCase);

            // 1. Local / legacy connection string (Azulite or real key)
            if (!string.IsNullOrWhiteSpace(raw) && LooksLikeConnString(raw))
            {
                serviceClient = new TableServiceClient(raw);
                mode = raw.Equals("UseDevelopmentStorage=true", StringComparison.OrdinalIgnoreCase)
                    ? "ConnString-Azurite"
                    : "ConnString";
            }
            // 2. Identity with explicit tableServiceUri
            else if (!string.IsNullOrWhiteSpace(tableServiceUri) &&
                    Uri.TryCreate(tableServiceUri, UriKind.Absolute, out var tsUri))
            {
                serviceClient = new TableServiceClient(tsUri, new DefaultAzureCredential());
                mode = "AAD-tableServiceUri";
            }
            // 3. Identity with accountName
            else if (!string.IsNullOrWhiteSpace(accountName))
            {
                var endpointUri = new Uri($"https://{accountName}.table.core.windows.net");
                serviceClient = new TableServiceClient(endpointUri, new DefaultAzureCredential());
                mode = "AAD-accountName";
            }
            else
            {
                _logger.LogError("No storage configuration found (need AzureWebJobsStorage OR AzureWebJobsStorage__tableServiceUri OR AzureWebJobsStorage__accountName).");
                return new ObjectResult(new
                {
                    success = false,
                    error = "Storage configuration missing."
                }) { StatusCode = 500 };
            }

            switch (action)
            {
                case "createTable":
                    return await CreateTable(req, serviceClient);
                case "deleteTable":
                    return await DeleteTable(req, serviceClient);
                case "upsertEntity":
                    return await UpsertEntity(req, serviceClient);
                case "getEntity":
                    return await GetEntity(req, serviceClient);
                case "deleteEntity":
                    return await DeleteEntity(req, serviceClient);
                case "query":
                    return await QueryEntities(req, serviceClient);
                default:
                    return Bad($"Unsupported action '{action}'");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing action {Action}", action);
            return new ObjectResult(new { success = false, action, error = ex.Message }) { StatusCode = 500 };
        }
    }

    // --- Actions ---

    private async Task<IActionResult> CreateTable(HttpRequest req, TableServiceClient sc)
    {
        var table = GetRequired(req, "table");
        await sc.CreateTableIfNotExistsAsync(table);
        return Ok(new { success = true, action = "createTable", table, created = true });
    }

    private async Task<IActionResult> DeleteTable(HttpRequest req, TableServiceClient sc)
    {
        var table = GetRequired(req, "table");
        await sc.DeleteTableAsync(table);
        return Ok(new { success = true, action = "deleteTable", table });
    }

    private async Task<IActionResult> UpsertEntity(HttpRequest req, TableServiceClient sc)
    {
        var table = GetRequired(req, "table");
        var (bodyJson, dict) = await ReadBodyAsDictionary(req);

        string partitionKey = GetOptional(req, "partitionKey") ?? GetOptional(dict, "partitionKey") ?? "demoPartition";
        string rowKey = GetOptional(req, "rowKey") ?? GetOptional(dict, "rowKey") ?? Guid.NewGuid().ToString("n");

        // Remove system keys from user props
        var entity = new TableEntity(partitionKey, rowKey);
        foreach (var kv in dict.Where(k => !IsSystemProp(k.Key)))
        {
            entity[kv.Key] = kv.Value ?? string.Empty;
        }

        var tc = sc.GetTableClient(table);
        await tc.UpsertEntityAsync(entity, TableUpdateMode.Merge);

        return Ok(new
        {
            success = true,
            action = "upsertEntity",
            table,
            partitionKey,
            rowKey,
            properties = entity.Keys.Where(k => k is not "PartitionKey" and not "RowKey" and not "Timestamp" and not "ETag"),
            body = bodyJson
        });
    }

    private async Task<IActionResult> GetEntity(HttpRequest req, TableServiceClient sc)
    {
        var table = GetRequired(req, "table");
        var pk = GetRequired(req, "partitionKey");
        var rk = GetRequired(req, "rowKey");

        var tc = sc.GetTableClient(table);
        try
        {
            var resp = await tc.GetEntityAsync<TableEntity>(pk, rk);
            var e = resp.Value;
            return Ok(new
            {
                success = true,
                action = "getEntity",
                table,
                partitionKey = pk,
                rowKey = rk,
                etag = e.ETag.ToString(),
                timestamp = e.Timestamp,
                properties = e.Where(p => !IsSystemProp(p.Key)).ToDictionary(p => p.Key, p => p.Value)
            });
        }
        catch (RequestFailedException rfe) when (rfe.Status == 404)
        {
            return NotFound(new { success = false, action = "getEntity", table, partitionKey = pk, rowKey = rk, error = "Not found" });
        }
    }

    private async Task<IActionResult> DeleteEntity(HttpRequest req, TableServiceClient sc)
    {
        var table = GetRequired(req, "table");
        var pk = GetRequired(req, "partitionKey");
        var rk = GetRequired(req, "rowKey");

        var tc = sc.GetTableClient(table);
        try
        {
            await tc.DeleteEntityAsync(pk, rk);
            return Ok(new { success = true, action = "deleteEntity", table, partitionKey = pk, rowKey = rk });
        }
        catch (RequestFailedException rfe) when (rfe.Status == 404)
        {
            return NotFound(new { success = false, action = "deleteEntity", table, partitionKey = pk, rowKey = rk, error = "Not found" });
        }
    }

    private async Task<IActionResult> QueryEntities(HttpRequest req, TableServiceClient sc)
    {
        var table = GetRequired(req, "table");
        var filter = GetOptional(req, "filter");
        var topStr = GetOptional(req, "top");
        int? top = int.TryParse(topStr, out var tParsed) ? tParsed : null;

        var tc = sc.GetTableClient(table);

        var results = new List<object>();
        int count = 0;
        await foreach (var e in tc.QueryAsync<TableEntity>(filter: filter))
        {
            results.Add(new
            {
                partitionKey = e.PartitionKey,
                rowKey = e.RowKey,
                timestamp = e.Timestamp,
                properties = e.Where(p => !IsSystemProp(p.Key)).ToDictionary(p => p.Key, p => p.Value)
            });
            count++;
            if (top.HasValue && count >= top.Value) break;
        }

        return Ok(new
        {
            success = true,
            action = "query",
            table,
            filter,
            count = results.Count,
            items = results
        });
    }

    // --- Helpers ---

    private static bool IsSystemProp(string k) =>
        k is "PartitionKey" or "RowKey" or "Timestamp" or "ETag";

    private static string? GetOptional(HttpRequest req, string key) => req.Query[key].FirstOrDefault();
    private static string? GetOptional(IDictionary<string, object?> dict, string key) =>
        dict.TryGetValue(key, out var v) ? v?.ToString() : null;

    private static string GetRequired(HttpRequest req, string key)
    {
        var v = GetOptional(req, key);
        if (string.IsNullOrWhiteSpace(v)) throw new ArgumentException($"Missing required parameter '{key}'");
        return v;
    }

    private async Task<(string raw, Dictionary<string, object?> dict)> ReadBodyAsDictionary(HttpRequest req)
    {
        if (req.Body == null || !req.Body.CanRead) return ("", new());
        using var reader = new StreamReader(req.Body);
        var raw = await reader.ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(raw)) return (raw, new());

        try
        {
            var doc = JsonSerializer.Deserialize<Dictionary<string, object?>>(raw, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                NumberHandling = JsonNumberHandling.AllowReadingFromString
            }) ?? new Dictionary<string, object?>();
            return (raw, doc);
        }
        catch
        {
            return (raw, new Dictionary<string, object?> { { "raw", raw } });
        }
    }

    // --- Result helpers ---

    private IActionResult Ok(object o) => new OkObjectResult(o);
    private IActionResult Bad(string msg) => new BadRequestObjectResult(new { success = false, error = msg });
    private IActionResult NotFound(object o) => new NotFoundObjectResult(o);
}
