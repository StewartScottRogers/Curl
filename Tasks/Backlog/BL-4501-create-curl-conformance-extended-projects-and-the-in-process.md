---
id: BL-4501
title: Create Curl.Conformance.Extended projects and the in-process curl harness with TCP, TLS and UDP routes and a scripted reference server
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.Extended.UnitLibrary/Harness, Curl.Conformance.Extended.UnitLibrary/Curl.Conformance.Extended.UnitLibrary.csproj, Curl.Conformance.Extended.UnitTests/Harness, Curl.Conformance.Extended.UnitTests/Curl.Conformance.Extended.UnitTests.csproj, Curl.slnx, Curl.Console/Curl.Console.csproj, Curl.Mcp/Curl.Mcp.csproj, Curl.Mcp.UnitLibrary/Curl.Mcp.UnitLibrary.csproj]
requirement: none
created: 2026-11-02
completed:
---
# BL-4501 — Create Curl.Conformance.Extended projects and the in-process curl harness with TCP, TLS and UDP routes and a scripted reference server

## Goal

The new projects `Curl.Conformance.Extended.UnitLibrary` and `Curl.Conformance.Extended.UnitTests` run a real curl command line in process against in-memory reference servers over TCP, TLS and UDP routes and report the exit code, stdout, stderr and each side's wire bytes per connection or datagram channel.

## Context

Split from BL-4462; read BL-4462 (in `Tasks/Deferred`) for the full plan and the contract the 20 per-protocol tasks are written against: layout (`Harness/` in both projects, `<Name>/` per protocol, `CurlSurface/`), no central registry, the run API (reference server(s) per host and port, curl arguments, optional environment variables, optional `TimeProvider`), and the scripted reference server that replays a fixed byte script and records what the client sent.

Start from `Curl.Console/InProcessCurl.cs` (`RunAsync` with `ITcpDialer`, `IDnsResolver`, `IDatagramConnector`, `readEnvironmentVariable`) and reuse `Curl.Conformance.UnitLibrary` (`InMemoryDuplexStream`, `HttpsServerConnector` and `TlsServerStream` for in-memory TLS) rather than copying it; `Curl.Conformance.UnitTests/InMemoryServerTcpDialer.cs` and `LoopbackOnlyDnsResolver.cs` show the dialer and resolver pattern (they live in a test project, so the library writes its own).

`VerifyExtensionIsolation` (do not change `Directory.Build.props`): the UnitTests project references the hosts `Curl.Console` and `Curl.Mcp`; the UnitLibrary references only `Curl.Protocol.Abstractions`, `Curl.Networking`, `Curl.Conformance.UnitLibrary`, the conformance servers and the shared layers, never an extension. Add `Curl.Conformance.Extended.UnitTests` to the `InternalsVisibleTo` lists of `Curl.Console/Curl.Console.csproj`, `Curl.Mcp/Curl.Mcp.csproj` and `Curl.Mcp.UnitLibrary/Curl.Mcp.UnitLibrary.csproj`. Solution placement: one `<Folder Name="/Conformance/Extended/">` holding both projects in `Curl.slnx` (ADR-0731). Use the `new-project` skill. The run's `TimeProvider` reaches curl in BL-4503, once BL-4500 lands.

## Acceptance criteria

- [ ] `Curl.Conformance.Extended.UnitLibrary` and `Curl.Conformance.Extended.UnitTests` exist, sit together in `/Conformance/Extended/` in `Curl.slnx`, and the UnitLibrary references no `Curl.Extension.*` or `Curl.Extended.Protocol.*` project; `Directory.Build.props` is unchanged.
- [ ] `Curl.Conformance.Extended.UnitTests` is in the `InternalsVisibleTo` lists of `Curl.Console`, `Curl.Mcp` and `Curl.Mcp.UnitLibrary`.
- [ ] A test in `Curl.Conformance.Extended.UnitTests/Harness/` runs a real curl command line in process (for example `redis://` against the scripted server) and asserts its exit code, stdout, stderr and the client's wire bytes.
- [ ] Tests in `Harness/` cover a TCP, a TLS and a UDP route and an environment variable reaching the run (`CURL_EXTENSIONS=0` gives the unsupported-scheme exit code 1), with no real socket, file outside a temporary directory, or process.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests are green; no test needs `TestCategory=Integration`.
- [ ] `Curl.Conformance.Extended.UnitLibrary` is at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`).
- [ ] No package added; every test is platform-neutral (no drive letters or Windows-only text).

## Notes

- 2026-11-02 lane 1 (run 1, handed back at the cost cap; nothing committed, the shift stashes the code):
  - Done: both projects, `/Conformance/Extended/` in `Curl.slnx`, the three `InternalsVisibleTo` lines,
    the UnitLibrary's `CLAUDE.md`, and `Harness/`: `ExtendedConformanceHarness.RunAsync` takes the curl entry
    point as an `InProcessCurlRunner` delegate (the UnitLibrary cannot reference `Curl.Console`; tests pass
    `InProcessCurl.RunAsync`), `ReferenceRoute` (Tcp/Tls/Udp by port; every host resolves to 127.0.0.1),
    `ScriptedReferenceServer`/`ScriptStep`, `ScriptedDatagramServer`, `WireExchange`, `ReferenceConnection`
    (records at the server, so a TLS route records plaintext), `ReferenceCertificate`. TimeProvider left to BL-4503.
  - Measured: `redis://` sends RESP3 `HELLO 3` before the command; the script answers `%0` then `+PONG`.
    TCP (redis), UDP (tftp), `CURL_EXTENSIONS=0` (exit 1), refused port (exit 7) and the dialer/channel tests pass.
  - Left: `RunAsync_RedisPingOverTls_...` hangs or crashes the test host (`rediss://` with `-k`). Suspect
    `ReferenceTcpDialer.ServeAsync`'s TLS branch (no ALPN in `TlsServerOptions`; a failed accept is swallowed by
    `ReferenceRun.EndAsync`). Then verify the TCP test's `PONG\n` stdout, build the solution `-warnaserror`, fast
    tests, one `Measure-CodeQuality.ps1 -Library Curl.Conformance.Extended.UnitLibrary`, and an ADR for the harness.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 94bce983 a163e683 ff67eeb1 07a99046 34c192a5 cc077f22 5306de6c.
- 2026-11-02: Doing -> Backlog. Cost cap: harness, projects and TCP/UDP routes written (uncommitted, stashed); TLS route test hangs, quality run and ADR left
- 2026-11-02: Backlog -> Deferred. Split at the cost cap into BL-4505, BL-4506 and BL-4507
- 2026-11-02: Deferred -> Backlog. Stewart 2026-11-02: complete all existing tasks before the third-party handoff.
