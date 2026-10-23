# ADR-0791: A Curl.Api request removes a default header through `RemovedHeaders`

- **Status:** Accepted
- **Date:** 2026-10-23

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-3294.

## Context

curl's `-H 'Accept:'` (a `CURLOPT_HTTPHEADER` line `Name:`) removes a header libcurl adds by
default. `CurlRequest.Headers` produces only `Name: value` and `Name;` (empty value), so a
`Curl.Api` request could not express removal.

## Decision

`CurlRequest.RemovedHeaders` is a list of header names (default none). Each becomes the line
`Name:` after the `Headers` lines. `Headers` keeps its meaning: an empty value still sends the
header with no value (`Name;`). A separate list, not a sentinel value such as null, keeps
`Headers` a plain list of fields and makes removal explicit.

## Consequences

`Curl.Api` can replay `-H 'Name:'` differential cases. The `HttpMessageHandler` mapping is
unchanged: `HttpRequestMessage` has no way to ask for removal.
