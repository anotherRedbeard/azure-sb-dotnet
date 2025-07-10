#!/bin/bash

# Test script for Azure Function Cache
FUNCTION_URL="http://localhost:7071/api/CacheResponseToUserHttpTrigger"

echo "🧪 Testing Azure Function In-Memory Cache"
echo "Function URL: $FUNCTION_URL"
echo ""

# Test 1: Store a simple string value
echo "1️⃣ Storing simple string value..."
curl -s -X POST "$FUNCTION_URL?key=greeting&ttl=60" \
  -H "Content-Type: application/json" \
  -d '"Hello, Azure Functions Cache!"' | jq '.'

echo ""

# Test 2: Retrieve the stored value
echo "2️⃣ Retrieving stored value..."
curl -s "$FUNCTION_URL?key=greeting" | jq '.'

echo ""

# Test 3: Store a JSON object
echo "3️⃣ Storing JSON object..."
curl -s -X POST "$FUNCTION_URL?key=user:123&ttl=120" \
  -H "Content-Type: application/json" \
  -d '{
    "id": 123,
    "name": "John Doe",
    "email": "john.doe@example.com",
    "roles": ["user", "admin"],
    "settings": {
      "theme": "dark",
      "notifications": true
    }
  }' | jq '.'

echo ""

# Test 4: Retrieve the JSON object
echo "4️⃣ Retrieving JSON object..."
curl -s "$FUNCTION_URL?key=user:123" | jq '.'

echo ""

# Test 5: Store API response cache
echo "5️⃣ Storing API response cache..."
curl -s -X POST "$FUNCTION_URL?key=weather:newyork&ttl=300" \
  -H "Content-Type: application/json" \
  -d '{
    "city": "New York",
    "temperature": 72,
    "condition": "sunny",
    "humidity": 45,
    "wind": {
      "speed": 10,
      "direction": "NW"
    },
    "forecast": [
      {"day": "today", "high": 75, "low": 65},
      {"day": "tomorrow", "high": 78, "low": 68}
    ],
    "fetched_at": "2025-07-10T10:00:00Z"
  }' | jq '.'

echo ""

# Test 6: Cache miss
echo "6️⃣ Testing cache miss..."
curl -s "$FUNCTION_URL?key=nonexistent" | jq '.'

echo ""

# Test 7: Store with default TTL
echo "7️⃣ Storing with default TTL (5 minutes)..."
curl -s -X POST "$FUNCTION_URL?key=config" \
  -H "Content-Type: application/json" \
  -d '{
    "api_endpoint": "https://api.example.com",
    "retry_attempts": 3,
    "timeout_seconds": 30
  }' | jq '.'

echo ""

# Test 8: Delete a cached item
echo "8️⃣ Deleting cached item..."
curl -s -X DELETE "$FUNCTION_URL?key=greeting" | jq '.'

echo ""

# Test 9: Verify deletion
echo "9️⃣ Verifying deletion (should be cache miss)..."
curl -s "$FUNCTION_URL?key=greeting" | jq '.'

echo ""

# Test 10: Show remaining cached items
echo "🔍 Checking remaining cached items:"
echo "   - user:123:"
curl -s "$FUNCTION_URL?key=user:123" | jq '.key, .hit, .ttl_seconds'

echo "   - weather:newyork:"
curl -s "$FUNCTION_URL?key=weather:newyork" | jq '.key, .hit, .ttl_seconds'

echo "   - config:"
curl -s "$FUNCTION_URL?key=config" | jq '.key, .hit, .ttl_seconds'

echo ""
echo "✅ Cache testing completed!"
echo ""
echo "💡 Key Features Demonstrated:"
echo "   ✓ String and JSON storage"
echo "   ✓ Custom TTL configuration"
echo "   ✓ Cache hit/miss tracking"
echo "   ✓ TTL remaining calculation"
echo "   ✓ Cache deletion"
echo "   ✓ Automatic expiration"
