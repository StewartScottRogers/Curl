# ADR-1335: A Curl.Api header with an empty value removes it; `EmptyHeaders` sends one empty

- Status: Accepted (2026-11-22, BL-5747)
- Decided by Claude under Stewart's delegation.

## Context

`curl -H "Name:"` removes a header and `curl -H "Name;"` sends it with no value. `Curl.Api` sent an
empty `Headers` value as `Name;`, so `new("grpc-timeout", "")` sent an empty `grpc-timeout` that a
server rejected, where `-H "grpc-timeout:"` removes it. BL-5598 already made the `curl-mcp`
`transfer` tool follow curl (ADR-1320): empty removes, `sendEmpty` sends empty.

## Decision

An empty value in `CurlRequest.Headers` is the `CURLOPT_HTTPHEADER` line `Name:`, which removes the
header, as `-H "Name:"` does. A new `CurlRequest.EmptyHeaders` list of names sends each header with
no value, as the line `Name;`, after `Headers` and before `RemovedHeaders`. `RemovedHeaders` is
unchanged.

## Consequences

A caller that relied on an empty `Headers` value sending an empty header moves that name to
`EmptyHeaders`.
