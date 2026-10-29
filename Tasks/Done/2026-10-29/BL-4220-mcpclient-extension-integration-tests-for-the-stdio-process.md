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
completed: 2026-10-29
---
# BL-4220 — McpClient extension: Integration tests for the stdio process starter

## Goal

`McpServerProcessStarter`, the `System.Diagnostics.Process` adapter BL-4216 left out of fast-run coverage, is exercised against a real child process by Integration tests.

## Context

BL-4216 added `Curl.Extension.McpClient.UnitLibrary/Stdio/McpServerProcessStarter.cs` behind `IMcpServerProcessStarter` and excluded it from coverage under ADR-0083 and ADR-1031, which expect such an adapter to be checked by an Integration run. No `Curl.Extension.McpClient.IntegrationTests` project exists yet: create it with the `new-project` skill (sorted directly before the library in `Curl.slnx`). The child process must be platform-neutral: for example a C# file-based app or the test host itself (`dotnet` with a small echo program) that reads newline-delimited JSON-RPC from stdin and answers on stdout - never `cmd.exe` or a POSIX shell alone.

## Acceptance criteria

- [x] `Curl.Extension.McpClient.IntegrationTests` exists, every test carries `[TestCategory("Integration")]`, and it is listed in `Curl.slnx`.
- [x] A test starts a real server through `McpServerProcessStarter`, sends a request with `McpStdioSession.SendRequestAsync` and receives its response.
- [x] A test shows `McpStdioSession.EndAsync` closing stdin ends a server that exits on end of input, and kills one that does not.
- [x] The tests pass on Windows and are platform-neutral; `dotnet build` is clean and the fast tests pass.

## Notes

- The stand-in server is a C# file-based app (`EchoMcpServer`) written to a new temporary directory and built there with `dotnet build <file> -o out` in `ClassInitialize`, then run as `dotnet <dll>`: the same child on Windows, Linux and macOS with no shell, and outside the repository so its `Directory.Build.props` does not apply. Chose a prebuilt dll over `dotnet run --file` so no build output can reach the session's standard output. It answers each request with its own process id, so the tests check the process is gone after `EndAsync`.
- `KillCountingMcpServerProcess` wraps the real process to tell a graceful exit (no kill) from a kill after `ExitGracePeriod`; the kill test waits the real 5 s grace period.
- Platform-neutral: no paths, OS error text or shell; the dotnet host comes from `DOTNET_HOST_PATH` or `dotnet` on PATH. Integration run on Windows: 3 passed. Solution build clean, fast tests green; no production code changed, so no quality run.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 51d91422 aa7a011d 3abafd76 d855b535.
- 2026-10-29: Doing -> Done. McpServerProcessStarter is exercised against a real child process by Curl.Extension.McpClient.IntegrationTests
