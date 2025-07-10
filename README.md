# Azure Functions: Policy Generator, Cache & Service Bus

Three simple Azure Functions for common scenarios.

## Functions

### 1. LLM Token Limit Policy Generator

Generates Azure API Management (APIM) policy XML for token limits based on subscription tier and model.

**Endpoint:** `GET /api/llm-policy`

**Parameters:**

- `tier` (optional): Subscription tier (free, standard, premium) - defaults to "free"
- `model` (optional): Model name (gpt-4, gpt-3.5-turbo, claude) - defaults to "gpt-3.5-turbo"

**Example:**

```bash
curl "http://localhost:7071/api/llm-policy?tier=premium&model=gpt-4"
```

### 2. In-Memory Cache

Simple REST API for key-value caching with TTL support.

**Endpoints:**

- `GET /api/cache?key=mykey` - Get value
- `POST /api/cache` - Set value (JSON: `{"key": "mykey", "value": "data", "ttl": 300}`)
- `DELETE /api/cache?key=mykey` - Delete value

**Features:**

- TTL (Time To Live) in seconds
- Hit/miss tracking
- JSON and text value support
- Thread-safe operations

**Example:**

```bash
# Set value with 5-minute TTL
curl -X POST "http://localhost:7071/api/cache" \
  -H "Content-Type: application/json" \
  -d '{"key": "test", "value": "hello world", "ttl": 300}'

# Get value
curl "http://localhost:7071/api/cache?key=test"

# Delete value
curl -X DELETE "http://localhost:7071/api/cache?key=test"
```

### 3. Service Bus Queue Trigger

Processes messages from Azure Service Bus queue with comprehensive logging and error handling.

**Trigger:** Service Bus Queue `my-test`

**Features:**

- Automatic message processing
- Processing duration tracking
- Comprehensive error logging
- Message retry on failure (abandon on error)
- Structured logging with multiple levels

**Configuration:**

- Queue name: `my-test`
- Connection string: `redccansbnamespace_SERVICEBUS` (set in local.settings.json)

**Processing flow:**

1. Receives message from queue
2. Logs message details (ID, body, content-type)
3. Tracks processing duration
4. Completes message on success
5. Abandons message on error (for retry)

## Quick Start

1. **Run locally:**

   ```bash
   cd src
   dotnet run
   ```

2. **Test functions:**

   ```bash
   # Test policy generator
   ./test-llm-policy.sh
   
   # Test cache
   ./test-cache.sh
   ```

## Design Decisions

### Why Simple?

- **No external dependencies**: Uses built-in .NET libraries
- **Minimal configuration**: Works out of the box
- **Clear endpoints**: Self-documenting API design
- **Error handling**: Basic but sufficient

### Policy Generator Design

- **Static mapping**: Predefined limits for common scenarios
- **String templates**: Simple XML generation
- **Query parameters**: Easy to use and test

### Cache Design

- **In-memory only**: No persistence complexity
- **IMemoryCache**: Built-in, thread-safe, with TTL
- **REST API**: Standard HTTP methods
- **JSON responses**: Structured data with metadata

### Service Bus Design

- **Queue-based**: Simple message processing pattern
- **Built-in retry**: Abandon on error for automatic retry
- **Comprehensive logging**: Multiple log levels for debugging
- **Duration tracking**: Measures processing time
- **Error handling**: Graceful failure with detailed logging

## Limitations

### Policy Generator Limitations

- Fixed tier/model combinations
- No validation of APIM schema
- Static XML templates only

### Cache Limitations

- **Memory-only**: Data lost on restart
- **Single instance**: No distributed caching
- **No persistence**: Not suitable for critical data
- **Memory limits**: Bounded by available RAM

### Service Bus Limitations

- **Single queue**: Hardcoded queue name `my-test`
- **Basic processing**: No complex message routing
- **Connection dependency**: Requires Service Bus namespace
- **No dead letter handling**: Uses default retry behavior

## Files

- `SimpleLlmPolicyGenerator.cs` - Policy generator function
- `CacheResponseToUserHttpTrigger.cs` - Cache function
- `ServiceBusQueueTrigger1.cs` - Service Bus queue processor
- `Program.cs` - DI configuration
- `test-llm-policy.sh` - Policy generator tests
- `test-cache.sh` - Cache function tests

## When to Use

### Policy Generator Use Cases

- Prototyping APIM policies
- Testing different token limits
- Generating policy templates

### Cache Use Cases

- Temporary data storage
- Session caching
- API response caching
- Development/testing scenarios

### Service Bus Use Cases

- Message queue processing
- Asynchronous task handling
- Event-driven architectures
- Background job processing
- Decoupled system communication

**Production Note:** For production use, consider Azure Redis Cache or Azure SQL for persistence and scaling.
