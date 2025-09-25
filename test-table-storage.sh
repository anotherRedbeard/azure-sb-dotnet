#!/usr/bin/env bash
set -euo pipefail

BASE="http://localhost:7071/api/TableStorageHttpTrigger"

pass() { echo "[PASS] $1"; }
fail() { echo "[FAIL] $1"; exit 1; }

jq_check() {
  local json="$1" expr="$2" expected="$3" label="$4"
  local got
  got=$(echo "$json" | jq -r "$expr")
  if [[ "$got" == "$expected" ]]; then pass "$label"; else echo "$json"; fail "$label (expected $expected got $got)"; fi
}

echo "Create table"
resp=$(curl -s "$BASE?action=createTable&table=demo")
echo $resp
jq_check "$resp" '.success' true "createTable success"

echo "Upsert entity"
resp=$(curl -s -X POST "$BASE?action=upsertEntity&table=demo" -H "Content-Type: application/json" -d '{"partitionKey":"p1","rowKey":"r1","n":1}')
jq_check "$resp" '.partitionKey' p1 "upsert partitionKey"

echo "Get entity"
resp=$(curl -s "$BASE?action=getEntity&table=demo&partitionKey=p1&rowKey=r1")
jq_check "$resp" '.rowKey' r1 "getEntity rowKey"

echo "Query"
resp=$(curl -s "$BASE?action=query&table=demo&filter=PartitionKey%20eq%20%27p1%27")
jq_check "$resp" '.count' 1 "query count"

echo "Delete entity"
resp=$(curl -s "$BASE?action=deleteEntity&table=demo&partitionKey=p1&rowKey=r1")
jq_check "$resp" '.success' true "deleteEntity success"

code=$(curl -s -o /dev/null -w "%{http_code}\n" "$BASE?action=getEntity&table=demo&partitionKey=p1&rowKey=r1")
[[ "$code" == "404" ]] && pass "404 after delete" || fail "Expected 404 after delete"

echo "Delete table"
resp=$(curl -s "$BASE?action=deleteTable&table=demo")
jq_check "$resp" '.action' deleteTable "deleteTable action"

echo "All tests passed."
