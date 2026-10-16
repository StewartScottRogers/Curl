---
id: BL-2381
title: Run a curl command line as the curl tool: argument array in, stdout, stderr and exit code out
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-2321, BL-2379, BL-2380]
touches: [Curl.Mcp.UnitLibrary, Curl.Mcp.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---

## Goal

Implement the logic of MCP tool `curl` in Curl.Mcp.UnitLibrary behind an injected runner abstraction.

## Context

ADR-0548 (`Documentation/Planning/Decisions/ADR-0548-curl-mcp-an-mcp-server-over-the-api-seam-on-microsofts-sdk.md`) and the plan `Documentation/Planning/Curl-Mcp.md` decide the design: `Curl.Mcp` is the thin host holding the Microsoft MCP SDK wiring, `Curl.Mcp.UnitLibrary` holds all tool logic (base class library only), `Curl.Mcp.UnitTests` tests both. The two packages `ModelContextProtocol` 2.2.0 and `Microsoft.Extensions.Hosting` 10.0.x are approved for `Curl.Mcp` only.

The in-process runner today is `Curl.Console.InProcessCurl.RunAsync(arguments, stdout, stderr, stdin, connector, datagramConnector)` (an exe's public static class). The UnitLibrary must not reference `Curl.Console`; define `ICurlCommandRunner` here (arguments, three streams, working directory, token to exit code) and let the next task adapt `InProcessCurl` to it in `Curl.Mcp`. The tool takes `args` (string array, without the program name) and optional `stdin` text. It first runs the confinement checker, then the runner with the root as working directory (pass it through the runner; never change the process-wide current directory on a server), standard input empty unless given, standard output and error captured to memory bounded by the cap, then returns stdout, stderr, exit code and the truncation flags. `--ai-help` and `--help` output pass through unchanged.

## Acceptance criteria

- [x] With a fake runner a test asserts stdout, stderr and exit code are returned for exit 0, and for exit 22 a tool error carrying curl's stderr message.
- [x] A confinement violation never reaches the runner (the fake records zero calls) and the error names the option; a missing `CURL_MCP_ROOT` refuses file-touching arguments but still allows an ordinary transfer to stdout (a test pins both).
- [x] `args` exclude the program name, and a test pins that a leading `curl` argument is passed on as a URL, as curl would treat it.
- [x] Output larger than the cap is truncated with the note; the request token reaches the runner (the fake observes it).
- [x] Arguments containing `--next`, `-w` and `--ai-help` and a very long argument list work through the fake.
- [x] - `dotnet build Curl.Mcp -warnaserror` and `dotnet build Curl.Mcp.UnitLibrary -warnaserror` are clean; `dotnet test Curl.Mcp.UnitTests --filter "TestCategory!=Integration"` is green; `powershell -NoProfile -File Measure-CodeQuality.ps1` reports no member of `Curl.Mcp.UnitLibrary` or `Curl.Mcp` outside the four gates (100% line, 100% branch, complexity at most 10, CRAP at most 30).

## Notes

Plan: Documentation/Planning/Curl-Mcp.md. Decision: ADR-0548, and ADR-0565 for this tool's shape.

- Built directly (no protocol-architect stage): the plan and ADR-0548/0555/0564 already fixed the design, so a planning sub-agent would cost more than it saves.
- `ICurlCommandRunner` takes the arguments, three streams, a nullable working directory (the resolved root; null when CURL_MCP_ROOT is unset, since the checker has refused every file path then) and the token, and gives the exit code.
- `McpCappedCaptureStream` keeps cap+1 bytes (so the UTF-8 back-off can see the byte past the cut) and counts every byte; `McpReturnedBytes.Encode(kept, totalBytes, cap)` was added so the note states the full size.
- `McpCurlToolResult` carries stdout, stderr, exit code and a failure together, because on a nonzero exit both streams still matter; binary stderr leaves the failure text as the exit-code line only.
- Measured: Measure-CodeQuality.ps1 -Library Curl.Mcp.UnitLibrary,Curl.Mcp - Curl.Mcp 100/100; Curl.Mcp.UnitLibrary 100% line, two null-branch gaps in McpCurlToolResult's truncation flags, closed by asserting them on a refused result. Worst CRAP 10.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. MCP curl tool logic: McpCurlTool checks arguments against CURL_MCP_ROOT, runs them through ICurlCommandRunner and returns capped stdout, stderr and exit code
