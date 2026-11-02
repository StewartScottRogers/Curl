---
id: BL-4483
title: Conformance: MCP 2025-06-18 stdio transport for mcp+stdio:// (McpClient part 2 of 2)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4501, BL-4502, BL-4503, BL-4482, BL-4463]
touches: [Curl.Conformance.Extended.UnitLibrary/McpClient/Stdio, Curl.Conformance.Extended.UnitTests/McpClient/Stdio, Documentation/Conformance/Extended/McpClient-Stdio.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4483 — Conformance: MCP 2025-06-18 stdio transport for mcp+stdio:// (McpClient part 2 of 2)

## Goal

Curl's `mcp+stdio://` client is measured against the Model Context Protocol 2025-06-18 stdio transport through the
real curl command line, with an in-memory stand-in process, every client requirement is in a matrix mapped to a
test, and every failure is fixed or listed with its fix task.

## Context

Specification: Model Context Protocol, revision 2025-06-18, "Base Protocol > Transports > stdio" (the client
launches the server as a subprocess; newline-delimited JSON-RPC on stdin and stdout; messages must not contain
embedded newlines; the server may log to stderr; nothing but MCP messages on stdout; shutdown by closing stdin, then
waiting, then terminating) and "Lifecycle > Shutdown". Builds on the reference MCP server BL-4482 wrote in
`Curl.Conformance.Extended.UnitLibrary/McpClient/`; serve it over stdio from `McpClient/Stdio/`. BL-4463 lets the
harness hand the handler a stand-in process starter; use it, so no test starts a real process.

Library: `Curl.Extended.Protocol.McpClient.UnitLibrary` (`Stdio/McpStdioSession`, `McpLineReader`,
`McpServerProcessStarter`, `Client/McpStdioClientSession`, `McpStdioCommand`). URL form:
`mcp+stdio://localhost/command?arg1&arg2`.

Part of the extended-protocol conformance plan Stewart approved on 2026-11-02. Follow
`Documentation/Conformance/Extended/README.md` and the ADR BL-4462 records: the harness, `[SpecRequirement]` IDs
(`McpClient:<source>-<locator>`), the matrix format and the `KnownFailures.txt` rule. Measure against the
specification, never against upstream curl. One file per case group; nothing added to `Harness/`. This task touches
only its own conformance files so protocols run in parallel: a fault in its own server or cases is fixed here, but a
defect in the extension library is listed in `KnownFailures.txt` with a filed High task
`Fix McpClient conformance: <requirement>` (pipeline feature; touches the library, its `.UnitTests` and
`KnownFailures.txt`; criteria: the case passes, a focused unit test in the library's `.UnitTests` pins the fix
(ADR-0482), the `KnownFailures.txt` line is removed).

## Acceptance criteria

- [ ] `Documentation/Conformance/Extended/McpClient-Stdio.md` gives every MUST, MUST NOT, SHOULD and SHOULD NOT of
      the stdio transport and shutdown that binds a client a `McpClient:` ID mapped to a test or to `n/a: <reason>`;
      the harness's matrix check passes.
- [ ] Cases in `Curl.Conformance.Extended.UnitTests/McpClient/Stdio/` run curl in process against the stand-in
      process and assert the program name and arguments started, the bytes written to its stdin, stdout, stderr and
      exit code for at least: list and call, each message one line with no embedded newline, server log lines on
      its stderr, a server that exits early, a server that writes a non-JSON line, and stdin closed at the end.
- [ ] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`
      with the ID of its filed High `Fix McpClient conformance: ...` task; the known-failures check passes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is
      green; no test needs `TestCategory=Integration` and none starts a real process.
- [ ] The code added under `Curl.Conformance.Extended.UnitLibrary/McpClient/Stdio/` is at 100% line and branch
      coverage, complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`).
- [ ] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).

## Notes

## Log

- 2026-11-02: Created.
