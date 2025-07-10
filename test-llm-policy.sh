#!/bin/bash

# Simple test script for LLM Token Limit Policy Generator
FUNCTION_URL="http://localhost:7071/api/SimpleLlmPolicyGenerator"

echo "🧠 Testing Simple LLM Token Limit Policy Generator"
echo "Function URL: $FUNCTION_URL"
echo ""

# Test different subscription tiers and models
echo "🔬 Testing different subscription tiers and models..."
echo ""

# Test 1: Premium GPT-4
echo "1️⃣ Premium tier with GPT-4:"
curl -s "$FUNCTION_URL?tier=premium&model=gpt-4" | jq '.'

echo ""

# Test 2: Business GPT-3.5
echo "2️⃣ Business tier with GPT-3.5:"
curl -s "$FUNCTION_URL?tier=business&model=gpt-3.5-turbo" | jq '.'

echo ""

# Test 3: Developer Claude
echo "3️⃣ Developer tier with Claude:"
curl -s "$FUNCTION_URL?tier=developer&model=claude-3" | jq '.'

echo ""

# Test 4: Standard (default)
echo "4️⃣ Standard tier (default):"
curl -s "$FUNCTION_URL" | jq '.'

echo ""

# Test 5: Just get the policy XML
echo "5️⃣ Policy XML for Premium GPT-4:"
curl -s "$FUNCTION_URL?tier=premium&model=gpt-4" | jq -r '.policy_xml'

echo ""
echo "✅ Testing completed!"
echo ""
echo "💡 To use in APIM, copy the PolicyXml content into your policy editor"
