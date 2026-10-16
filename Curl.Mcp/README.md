# Curl.Mcp

An MCP server that runs curl for AI agents. It serves Curl, the drop-in C# replacement for the
curl command-line tool (<https://github.com/StewartScottRogers/Curl>), over standard input and
output.

## Run it

```
dnx Curl.Mcp
```

## Environment

| Variable | Required | Meaning |
| --- | --- | --- |
| `CURL_MCP_ROOT` | yes | Every file curl reads or writes must lie inside this directory. |
| `CURL_MCP_MAX_RESPONSE_BYTES` | no | The most bytes of output a tool returns; 1048576 when unset. |
