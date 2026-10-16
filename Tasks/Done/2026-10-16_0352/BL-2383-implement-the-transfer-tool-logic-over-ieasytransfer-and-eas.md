---
id: BL-2383
title: Implement the transfer tool logic over IEasyTransfer and EasyOptions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2321, BL-2380]
touches: [Curl.Mcp.UnitLibrary, Curl.Mcp.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-16
---

## Goal

Implement, in Curl.Mcp.UnitLibrary, the logic of MCP tool `transfer`: a structured request in, status, headers, body and TransferInfo out.

## Context

ADR-0548 (`Documentation/Planning/Decisions/ADR-0548-curl-mcp-an-mcp-server-over-the-api-seam-on-microsofts-sdk.md`) and the plan `Documentation/Planning/Curl-Mcp.md` decide the design: `Curl.Mcp` is the thin host holding the Microsoft MCP SDK wiring, `Curl.Mcp.UnitLibrary` holds all tool logic (base class library only), `Curl.Mcp.UnitTests` tests both. The two packages `ModelContextProtocol` 2.2.0 and `Microsoft.Extensions.Hosting` 10.0.x are approved for `Curl.Mcp` only.

Use `ICurlEngine` / `IEasyTransfer` (`Curl.Api.UnitLibrary/EngineContracts`), the option groups under `Curl.Api.UnitLibrary/Options` (`EasyOptions`, `EasyHttpOptions`, `EasyTlsOptions` and the rest), `TransferInfo` (`Info/TransferInfo.cs`) and `CurlCode`. Request: `url`, `method` (default GET), `headers` (name/value list), `body` (inline text or base64, never a file), `followRedirects`, `timeoutSeconds`, `insecure` (default false), `proxy` and `userAgent`. No option here reads or writes a local path, so the tool needs no confinement, and a TLS option naming a path is deliberately not offered. Response: HTTP status code, response headers (ordered, repeats kept), body (text or base64, capped per the errors task), the `TransferInfo` values as a named object (total time, connect time, effective URL, redirect count, sizes, HTTP version, primary IP and port), and on failure the `CurlCode` with its message. Tests use a fake `IEasyTransfer` that records the options set and fires the write and header callbacks.

## Acceptance criteria

- [x] A fake-engine test asserts each request field becomes the matching option (method, headers, body, redirects, timeout, insecure, proxy, user agent) and nothing else is set.
- [x] The response carries status, ordered headers, body and the `TransferInfo` values the fake supplies; a failing `CurlCode` (6, 7, 28, 60) becomes a tool error with libcurl's message; a body over the cap is truncated with the note; a binary body is base64.
- [x] Validation: an empty or non-URL `url`, an unsupported method token, a header without a name and a negative timeout each fail with an error naming the field, before the engine is called.
- [x] Cancellation through the token reaches the fake transfer and yields the cancelled error.
- [x] - `dotnet build Curl.Mcp -warnaserror` and `dotnet build Curl.Mcp.UnitLibrary -warnaserror` are clean; `dotnet test Curl.Mcp.UnitTests --filter "TestCategory!=Integration"` is green; `powershell -NoProfile -File Measure-CodeQuality.ps1` reports no member of `Curl.Mcp.UnitLibrary` or `Curl.Mcp` outside the four gates (100% line, 100% branch, complexity at most 10, CRAP at most 30).

## Notes

Plan: Documentation/Planning/Curl-Mcp.md. Decision: ADR-0548.

- Decisions recorded in ADR-0593 (decided by Claude under Stewart's delegation): `file:` URLs are
  refused (they reach local files; the confined `curl` tool offers them); the method maps as
  `-X`/`-d`/`-I` do; `insecure` is the origin's verification only, as `-k`; headers returned are the
  last response's; "libcurl's message" is the handle's `ErrorText` (`CURLOPT_ERRORBUFFER`), since the
  `curl_easy_strerror` table is in `Curl.Console`, which this library may not reference.
- New failure kind `McpToolFailureKind.InvalidRequest` (`Invalid <field>: <reason>`) for validation.
- Quality: Measure-CodeQuality.ps1 for Curl.Mcp.UnitLibrary and Curl.Mcp found one branch gap in
  `McpTransferInfo.HttpVersionText` (a `switch` on `long` emits an unreachable range branch); it is now
  a dictionary lookup with no branches. Everything else 100% line and branch, worst CRAP 10.

## Log

- 2026-10-15: Created.
- 2026-10-16: Backlog -> Doing.
- 2026-10-16: Doing -> Done. transfer tool logic over IEasyTransfer and EasyOptions, ADR-0593; build clean, fast tests green
