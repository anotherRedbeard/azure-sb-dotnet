using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Company.Function
{
    public class HttpTrigger1
    {
        private readonly ILogger<HttpTrigger1> _logger;

        public HttpTrigger1(ILogger<HttpTrigger1> logger)
        {
            _logger = logger;
        }

        [Function("HttpTrigger1")]
        public IActionResult Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            // Extract client information for dynamic policy configuration
            var clientInfo = ExtractClientInfo(req);

            var policyConfig = GenerateLlmTokenLimitPolicy(clientInfo, req);

            return new OkObjectResult(policyConfig);
        }

        private ClientInfo ExtractClientInfo(HttpRequest req)
        {
            return new ClientInfo
            {
                SubscriptionKey = req.Headers["Ocp-Apim-Subscription-Key"].FirstOrDefault(),
                ClientId = req.Query["client_id"].FirstOrDefault() ??
                          req.Headers["X-Client-Id"].FirstOrDefault(),
                UseCase = req.Query["useCase"].FirstOrDefault() ??
                          req.Headers["X-Use-Case"].FirstOrDefault() ?? "standard",
                ModelName = req.Query["model"].FirstOrDefault() ??
                           req.Headers["X-Model-Name"].FirstOrDefault() ?? "gpt-4",
                RequestId = System.Guid.NewGuid().ToString(),
                Timestamp = DateTimeOffset.UtcNow,
                IpAddress = req.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"
            };
        }

        private object GenerateLlmTokenLimitPolicy(ClientInfo clientInfo, HttpRequest req)
        {
            // Determine token limits based on subscription tier and model
            var tokenLimits = DetermineTokenLimits(clientInfo);

            return new
            {
                PolicyType = "llm-token-limit",
                Configuration = new
                {
                    tokens = tokenLimits.MaxTokens,
                    tokens_per_minute = tokenLimits.TokensPerMinute,
                    // Renewal period name from enum to string
                    renewal_period = tokenLimits.RenewalPeriod.ToString(),
                    estimate_prompt_tokens = tokenLimits.EstimatePromptTokens,
                    retry_after_header_name = "Retry-After",
                    retry_after_variable_name = "llmRetryAfter",
                    remaining_tokens_header_name = "X-RateLimit-Remaining-Tokens",
                    remaining_tokens_variable_name = "llmRemainingTokens"
                },
                ApplicableScopes = new[] { "api", "operation" },
                ClientInfo = clientInfo,
                Documentation = new
                {
                    PolicyName = "LLM Token Limit",
                    Description = "Arrests usage spikes by limiting language model tokens per calculated key",
                    Scope = "Use in the inbound section at any scope",
                    Reference = "https://learn.microsoft.com/en-us/azure/api-management/llm-token-limit-policy"
                }
            };
        }

        private TokenLimits DetermineTokenLimits(ClientInfo clientInfo)
        {
            // Base limits for different subscription tiers
            var baseLimits = clientInfo.UseCase?.ToLower() switch
            {
                "maxtokenshourly" => new TokenLimits
                {
                    MaxTokens = 3000,
                    TokensPerMinute = 20000,
                    RenewalPeriod = RenewalPeriod.Hourly,
                    EstimatePromptTokens = true
                },
                "tpmweekly" => new TokenLimits
                {
                    MaxTokens = 2000,
                    TokensPerMinute = 1000,
                    RenewalPeriod = RenewalPeriod.Weekly,
                    EstimatePromptTokens = true
                },
                "developer" => new TokenLimits
                {
                    MaxTokens = 1000,
                    TokensPerMinute = 750,
                    RenewalPeriod = RenewalPeriod.Monthly,
                    EstimatePromptTokens = true
                },
                _ => new TokenLimits
                {
                    MaxTokens = 5000,
                    TokensPerMinute = 500,
                    RenewalPeriod = RenewalPeriod.Yearly,
                    EstimatePromptTokens = true
                }
            };
            // Adjust limits based on model type
            var modelMultiplier = clientInfo.ModelName?.ToLower() switch
            {
                var model when model?.Contains("gpt-4") == true => 1.5, // Higher limits for GPT-4
                var model when model?.Contains("gpt-3.5") == true => 1.0, // Standard limits
                var model when model?.Contains("claude") == true => 1.2, // Slightly higher for Claude
                var model when model?.Contains("llama") == true => 0.8, // Lower for open source models
                _ => 1.0
            };

            return new TokenLimits
            {
                MaxTokens = (int)(baseLimits.MaxTokens * modelMultiplier),
                TokensPerMinute = (int)(baseLimits.TokensPerMinute * modelMultiplier),
                RenewalPeriod = baseLimits.RenewalPeriod,
                EstimatePromptTokens = baseLimits.EstimatePromptTokens
            };
        }

    }

    public class ClientInfo
    {
        public string? SubscriptionKey { get; set; }
        public string? ClientId { get; set; }
        public string? UseCase { get; set; }
        public string? ModelName { get; set; }
        public string RequestId { get; set; } = string.Empty;
        public DateTimeOffset Timestamp { get; set; }
        public string IpAddress { get; set; } = string.Empty;
    }

    public class TokenLimits
    {
        public int MaxTokens { get; set; }
        public int TokensPerMinute { get; set; }
        public RenewalPeriod RenewalPeriod { get; set; }
        public bool EstimatePromptTokens { get; set; }
    }

    public enum RenewalPeriod
    {
        Yearly = 31536000, // 1 year in seconds
        Monthly = 2592000, // 30 days in seconds
        Weekly = 604800, // 7 days in seconds
        Daily = 86400, // 1 day in seconds
        Hourly = 3600, // 1 hour in seconds
    }
}

