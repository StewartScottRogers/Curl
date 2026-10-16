---
id: BL-2385
title: Add the parse_url and build_url tools over the URL handle
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2321, BL-2376, BL-2380, BL-2436, BL-2437]
touches: [Curl.Mcp, Curl.Mcp.UnitLibrary, Curl.Mcp.UnitTests]
requirement: none
created: 2026-10-15
completed:
---

## Goal

Provide MCP tools parse_url (a URL to its parts) and build_url (parts to a URL) over ICurlUrlHandle.

## Context

ADR-0548 (`Documentation/Planning/Decisions/ADR-0548-curl-mcp-an-mcp-server-over-the-api-seam-on-microsofts-sdk.md`) and the plan `Documentation/Planning/Curl-Mcp.md` decide the design: `Curl.Mcp` is the thin host holding the Microsoft MCP SDK wiring, `Curl.Mcp.UnitLibrary` holds all tool logic (base class library only), `Curl.Mcp.UnitTests` tests both. The two packages `ModelContextProtocol` 2.2.0 and `Microsoft.Extensions.Hosting` 10.0.x are approved for `Curl.Mcp` only.

`ICurlUrlHandle` and `CurlUrlFlags` are in `Curl.Api.UnitLibrary/EngineContracts/Url`; `CurlUrlCode` is the failure code. Parts follow libcurl's URL API (https://curl.se/libcurl/c/libcurl-url.html): scheme, user, password, options, host, port, path, query, fragment, zoneid. `parse_url` accepts `url` and optional flags the handle supports; `build_url` accepts the parts and returns the URL string. Logic in the UnitLibrary over an injected handle factory; one thin method each in `Curl.Mcp`.

## Acceptance criteria

- [ ] Fake-handle tests: every part is returned and an absent part is reported as absent, not empty; `build_url` sets only the supplied parts; `CurlUrlCode` failures (bad URL, no host, bad port) become tool errors naming the code.
- [ ] One test per tool through the real handle: `https://user:pw@example.com:8443/a/b?x=1#f` parses to its parts and `build_url` of those parts returns the same URL.
- [ ] The server lists `parse_url` and `build_url` with descriptions.
- [ ] - `dotnet build Curl.Mcp -warnaserror` and `dotnet build Curl.Mcp.UnitLibrary -warnaserror` are clean; `dotnet test Curl.Mcp.UnitTests --filter "TestCategory!=Integration"` is green; `powershell -NoProfile -File Measure-CodeQuality.ps1` reports no member of `Curl.Mcp.UnitLibrary` or `Curl.Mcp` outside the four gates (100% line, 100% branch, complexity at most 10, CRAP at most 30).

## Notes

Plan: Documentation/Planning/Curl-Mcp.md. Decision: ADR-0548.

2026-10-16 (lane 6): requeued before any code. The acceptance criterion "one test per tool through the real handle" cannot be met yet: `ManagedCurlEngine.CreateUrl` throws `NotSupportedException` ("The URL API is phase 3 work"), and no production `ICurlUrlHandle` exists. BL-2436 (get over `CurlUrl`) and BL-2437 (set, dup, clear) build it; both are in Backlog, so they are added to `depends-on`. No other prerequisite found: the tool logic will take an injected `Func<ICurlUrlHandle>` (the engine's `CreateUrl`) in `Curl.Mcp.UnitLibrary`, failures map to an `McpToolFailure` naming the `CurlUrlCode`, and `Curl.Mcp` registers both tools in `CurlMcpHost` over `ManagedCurlEngine`.

## Log

- 2026-10-15: Created.
- 2026-10-16: Backlog -> Doing.
- 2026-10-16: Doing -> Backlog. Waits on BL-2436 and BL-2437: the engine has no real ICurlUrlHandle yet (CreateUrl throws), so the real-handle tests cannot pass
- 2026-10-16: Backlog -> Doing.
