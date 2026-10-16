---
id: BL-2380
title: Map curl failures to MCP tool errors and cap the size of what a tool returns
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-2321, BL-2376]
touches: [Curl.Mcp.UnitLibrary, Curl.Mcp.UnitTests]
requirement: none
created: 2026-10-15
completed:
---

## Goal

Add the shared result plumbing in Curl.Mcp.UnitLibrary: curl exit code and CurlCode to a tool error with curl's message, text-or-base64 output decisions, and a size cap with a truncation note.

## Context

ADR-0548 (`Documentation/Planning/Decisions/ADR-0548-curl-mcp-an-mcp-server-over-the-api-seam-on-microsofts-sdk.md`) and the plan `Documentation/Planning/Curl-Mcp.md` decide the design: `Curl.Mcp` is the thin host holding the Microsoft MCP SDK wiring, `Curl.Mcp.UnitLibrary` holds all tool logic (base class library only), `Curl.Mcp.UnitTests` tests both. The two packages `ModelContextProtocol` 2.2.0 and `Microsoft.Extensions.Hosting` 10.0.x are approved for `Curl.Mcp` only.

Tool errors in MCP are results flagged as errors with text, not protocol exceptions. A nonzero curl exit code is a tool error whose text is curl's own stderr message and the numeric code (https://curl.se/libcurl/c/libcurl-errors.html lists libcurl's codes; the exit codes are `CurlExitCode` in this repository); a refused path (confinement) is an error naming the option; a cancelled request is its own error. Output bytes: valid UTF-8 is returned as text, anything else as base64 with a flag saying so. The cap is set by `CURL_MCP_MAX_RESPONSE_BYTES` (default 1 MiB, read through an injected reader as in the root task) and truncation is stated in the result, never silent. Cancellation: the MCP request's `CancellationToken` is passed through; a cancelled run reports it.

## Acceptance criteria

- [ ] Types for the three results (a tool failure, returned bytes with text/base64 and truncated flags, the size cap) exist with tests for: exit 0 not an error, exits 6/7/22/28/35 produce errors containing curl's message and the code, every `CurlExitCode` value maps without throwing, valid UTF-8, invalid UTF-8 as base64, a multi-byte character cut at the cap (truncation never emits a broken sequence), exactly-at-cap not truncated, one byte over truncated with the note stating total and returned sizes.
- [ ] The cap setting rejects non-numeric, zero and negative values with a named error; a test pins that it fails the start rather than guessing.
- [ ] Cancellation: a token cancelled before and during a fake run yields the cancelled error and no partial output beyond the cap.
- [ ] - `dotnet build Curl.Mcp -warnaserror` and `dotnet build Curl.Mcp.UnitLibrary -warnaserror` are clean; `dotnet test Curl.Mcp.UnitTests --filter "TestCategory!=Integration"` is green; `powershell -NoProfile -File Measure-CodeQuality.ps1` reports no member of `Curl.Mcp.UnitLibrary` or `Curl.Mcp` outside the four gates (100% line, 100% branch, complexity at most 10, CRAP at most 30).

## Notes

Plan: Documentation/Planning/Curl-Mcp.md. Decision: ADR-0548.

## Log

- 2026-10-15: Created.
