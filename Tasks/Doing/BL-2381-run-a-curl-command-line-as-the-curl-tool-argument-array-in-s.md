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
completed:
---

## Goal

Implement the logic of MCP tool `curl` in Curl.Mcp.UnitLibrary behind an injected runner abstraction.

## Context

ADR-0548 (`Documentation/Planning/Decisions/ADR-0548-curl-mcp-an-mcp-server-over-the-api-seam-on-microsofts-sdk.md`) and the plan `Documentation/Planning/Curl-Mcp.md` decide the design: `Curl.Mcp` is the thin host holding the Microsoft MCP SDK wiring, `Curl.Mcp.UnitLibrary` holds all tool logic (base class library only), `Curl.Mcp.UnitTests` tests both. The two packages `ModelContextProtocol` 2.2.0 and `Microsoft.Extensions.Hosting` 10.0.x are approved for `Curl.Mcp` only.

The in-process runner today is `Curl.Console.InProcessCurl.RunAsync(arguments, stdout, stderr, stdin, connector, datagramConnector)` (an exe's public static class). The UnitLibrary must not reference `Curl.Console`; define `ICurlCommandRunner` here (arguments, three streams, working directory, token to exit code) and let the next task adapt `InProcessCurl` to it in `Curl.Mcp`. The tool takes `args` (string array, without the program name) and optional `stdin` text. It first runs the confinement checker, then the runner with the root as working directory (pass it through the runner; never change the process-wide current directory on a server), standard input empty unless given, standard output and error captured to memory bounded by the cap, then returns stdout, stderr, exit code and the truncation flags. `--ai-help` and `--help` output pass through unchanged.

## Acceptance criteria

- [ ] With a fake runner a test asserts stdout, stderr and exit code are returned for exit 0, and for exit 22 a tool error carrying curl's stderr message.
- [ ] A confinement violation never reaches the runner (the fake records zero calls) and the error names the option; a missing `CURL_MCP_ROOT` refuses file-touching arguments but still allows an ordinary transfer to stdout (a test pins both).
- [ ] `args` exclude the program name, and a test pins that a leading `curl` argument is passed on as a URL, as curl would treat it.
- [ ] Output larger than the cap is truncated with the note; the request token reaches the runner (the fake observes it).
- [ ] Arguments containing `--next`, `-w` and `--ai-help` and a very long argument list work through the fake.
- [ ] - `dotnet build Curl.Mcp -warnaserror` and `dotnet build Curl.Mcp.UnitLibrary -warnaserror` are clean; `dotnet test Curl.Mcp.UnitTests --filter "TestCategory!=Integration"` is green; `powershell -NoProfile -File Measure-CodeQuality.ps1` reports no member of `Curl.Mcp.UnitLibrary` or `Curl.Mcp` outside the four gates (100% line, 100% branch, complexity at most 10, CRAP at most 30).

## Notes

Plan: Documentation/Planning/Curl-Mcp.md. Decision: ADR-0548.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
