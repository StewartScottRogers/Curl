# ADR-1330: The transfer tool takes proxy login, proxy CA, resolve and a body-limit field (BL-5671)

Status: Accepted. Decided by Claude under Stewart's delegation.

## Context

An agent using `curl-mcp`'s `transfer` tool had to fall back to the `curl` tool to log in to a proxy,
trust a proxy's CA, pin a host to an address, or read a body past the 1 MiB response cap.

## Decision

- `proxyUser` (`user:password`, `CURLOPT_PROXYUSERPWD`), `proxyCaFile` (`CURLOPT_PROXY_CAINFO`) and
  `resolve` (array of `host:port:address`, `CURLOPT_RESOLVE`) are request fields. `proxyCaFile` is confined to
  `CURL_MCP_ROOT` as `caFile` is (BL-5596) and refused `PathRefused` naming `proxyCaFile`; when both CA
  files are refused the first, `caFile`, is named.
- `maxBodyBytes` sets the cap for that one request, 1 to `McpResponseCap.CeilingMaxBytes` (64 MiB); out of
  range is `InvalidRequest`. Omitted, the server's cap applies (1 MiB unless `CURL_MCP_MAX_RESPONSE_BYTES`).
  The ceiling bounds the memory one call may hold; the tool description and schema state the default and the ceiling.
- The proxy password is only ever placed in the handle's options: no check message, path refusal or result echoes it.

## Consequences

The three goldens of the tool's schema and description change with the fields.

## Amendment (BL-5688): the operator's limit bounds `maxBodyBytes`

Decided by Claude under Stewart's delegation. When `CURL_MCP_MAX_RESPONSE_BYTES` is set, it also caps a `transfer` call's `maxBodyBytes`: a value above it, up to 64 MiB, is clamped to it (the body is cut and `isTruncated` says so), so a client that asks for more than the operator allows still gets a truthful result. A smaller value still lowers the limit. The 64 MiB ceiling stays whatever the variable says: a `maxBodyBytes` above 67108864 is refused with `InvalidRequest` even when the variable is larger (BL-5689 chose to fix the text, not the code, so the real ceiling is the smaller of 64 MiB and the variable's value). `McpResponseCap.RequestCeilingBytes` holds the ceiling. The tool description and the `maxBodyBytes` schema text say so.
