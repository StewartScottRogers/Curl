# ADR-0659 — An easy handle without `CURLOPT_USERAGENT` sends no `User-Agent`

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2559.

## Context

`Curl.Original.Protocol.Http`'s `HttpRequestHeadFormatter` sends `User-Agent: curl/8.22.0` when
`HttpRequestOptions.UserAgent` is `null`, and `EngineComposition.CreateProxyTunnelOptions` gives a `CONNECT` the
same default. That default is the curl tool's: `tool_operate.c` sets `CURLOPT_USERAGENT` to `curl/<version>` when
`-A` is not given. libcurl 8.22.0 itself sends no `User-Agent` line without `CURLOPT_USERAGENT` (`lib/http.c` adds
one only when the option is set and not empty), and upstream's lib cases (test500 and most `libNNN` cases) expect
none. BL-2558 found a ported libtest program run over `ManagedCurlEngine` sending the tool's line.

The curl tool performs through the same `ManagedEasyTransfer`, always with its `ToolSettings` set
(`CurlCommandRunner`), and every RTSP, HTTP handler and `Curl.Console` test relies on the formatter's default.

## Decision

A perform from the options with no `ToolSettings` - a `Libcurl` or `Curl.Api` handle - runs with
`EasyHttpOptions.UserAgent` `null` turned into the empty string (`ManagedEasyTransfer.WithoutDefaultUserAgent`).
The formatter and the proxy tunnel already send no `User-Agent` for an empty one, as libcurl does for
`CURLOPT_USERAGENT ""`, so libcurl's "no line" needs no new option on `HttpRequestOptions` and no change in
`Curl.Original.Protocol.Http`. A perform with `ToolSettings` keeps `null`, and with it the tool's `curl/8.22.0`.

## Consequences

- `Libcurl`, `Curl.Api` and `Curl.Mcp` handles send `User-Agent` only when one is set (`CurlClientOptions.UserAgent`,
  `CURLOPT_USERAGENT`), to the origin and to a proxy's `CONNECT`.
- The curl tool's request bytes are unchanged.
- `Curl.Engine.UnitTests`' comparisons of a handle's perform with the runner's own context give the runner's side the
  same options the handle performs with.
