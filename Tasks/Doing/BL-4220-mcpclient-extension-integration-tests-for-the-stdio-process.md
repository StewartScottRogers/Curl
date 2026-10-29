---
id: BL-4220
title: McpClient extension: Integration tests for the stdio process starter
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-4216]
touches: [Curl.Extension.McpClient.IntegrationTests, Curl.slnx]
requirement: none
created: 2026-10-29
completed:
---
# BL-4220 — McpClient extension: Integration tests for the stdio process starter

## Goal

`McpServerProcessStarter`, the `System.Diagnostics.Process` adapter BL-4216 left out of fast-run coverage, is exercised against a real child process by Integration tests.

## Context

BL-4216 added `Curl.Extension.McpClient.UnitLibrary/Stdio/McpServerProcessStarter.cs` behind `IMcpServerProcessStarter` and excluded it from coverage under ADR-0083 and ADR-1031, which expect such an adapter to be checked by an Integration run. No `Curl.Extension.McpClient.IntegrationTests` project exists yet: create it with the `new-project` skill (sorted directly before the library in `Curl.slnx`). The child process must be platform-neutral: for example a C# file-based app or the test host itself (`dotnet` with a small echo program) that reads newline-delimited JSON-RPC from stdin and answers on stdout - never `cmd.exe` or a POSIX shell alone.

## Acceptance criteria

- [ ] `Curl.Extension.McpClient.IntegrationTests` exists, every test carries `[TestCategory("Integration")]`, and it is listed in `Curl.slnx`.
- [ ] A test starts a real server through `McpServerProcessStarter`, sends a request with `McpStdioSession.SendRequestAsync` and receives its response.
- [ ] A test shows `McpStdioSession.EndAsync` closing stdin ends a server that exits on end of input, and kills one that does not.
- [ ] The tests pass on Windows and are platform-neutral; `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 51d91422 aa7a011d 3abafd76 d855b535.
