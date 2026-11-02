---
id: BL-4463
title: Let a host inject the process starter the Plugin and McpClient stdio handlers use, so in-process conformance runs start no real process
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary/ProtocolHandlerServices.cs, Curl.Protocol.Abstractions.UnitLibrary/IExtensionProcessStarter.cs, Curl.Protocol.Abstractions.UnitTests/ProtocolHandlerServicesTests.cs, Curl.Engine.UnitLibrary/EngineComposition.cs, Curl.Engine.UnitTests/EngineCompositionTests.Extensions.cs, Curl.Extended.Protocol.Plugin.UnitLibrary/PluginProtocolExtension.cs, Curl.Extended.Protocol.Plugin.UnitTests, Curl.Extended.Protocol.McpClient.UnitLibrary/Client/McpClientExtension.cs, Curl.Extended.Protocol.McpClient.UnitTests, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-11-02
completed:
---
# BL-4463 — Let a host inject the process starter the Plugin and McpClient stdio handlers use, so in-process conformance runs start no real process

## Goal

A host that composes the engine can hand the extension handlers a process starter, the way it hands them the
connector today, so the Plugin (`curl-proto-<scheme>`) and McpClient (`mcp+stdio://`) handlers can be run through the
real curl command line against an in-memory stand-in process (and the Plugin search reads the host's `PATH`, not
the process environment), with the real `Process` starter and environment still the defaults.

## Context

Part of the extended-protocol conformance plan Stewart approved on 2026-11-02 (harness: BL-4462). The conformance
tasks for Plugin and for MCP's stdio transport run curl in process, and a unit test may not start a real process
(ADR-0421), but today both handlers hard-wire the real starter where the composed host cannot replace it:

- `Curl.Extended.Protocol.Plugin.UnitLibrary/PluginProtocolExtension.cs` line 36: `CreateHandler(ProtocolHandlerServices
  services, ...)` passes `new SystemHelperProcessStarter()` (an `IHelperProcessStarter`); `Create(...)` already takes
  the starter as a parameter.
  The same line also reads `PATH`/`PATHEXT` through `Environment.GetEnvironmentVariable` and lists directories
  through `HelperProgramDirectory.ListHelperFiles`, so a host cannot choose which `curl-proto-*` helpers exist
  either: the seam must cover the environment reader too (the directory listing may stay real, since a test may
  create helper files in a temporary directory it deletes, ADR-0421), so a run can put such a directory on its own
  `PATH` without changing the process's environment.
- `Curl.Extended.Protocol.McpClient.UnitLibrary/Client/McpClientExtension.cs` line 24: `CreateHandler` passes
  `new McpServerProcessStarter()` (an `IMcpServerProcessStarter`, `Stdio/`).
- The handlers get their transports from `ProtocolHandlerServices(Connector, DatagramConnector, TlsProvider,
  DnsResolver)` in `Curl.Protocol.Abstractions.UnitLibrary/ProtocolHandlerServices.cs`, built in
  `Curl.Engine.UnitLibrary/EngineComposition.cs` line 204.

Suggested shape (decide and record it): a process-starting contract in `Curl.Protocol.Abstractions`
(`IExtensionProcessStarter`: start a program with arguments and environment, give its standard input, output and error
streams and its exit) carried as an optional last member of `ProtocolHandlerServices` and an optional parameter of
`EngineComposition`, defaulting to a real starter; each extension adapts it to its own interface in its own files,
so neither extension references the other. Keep the real `Process` adapter excluded from coverage as
`SystemHelperProcessStarter` is (ADR-0083). Record the decision in a new ADR marked "Decided by Claude under Stewart's
delegation", one line in `Documentation/Planning/Decisions/README.md` (a shared list file).

## Acceptance criteria

- [ ] `ProtocolHandlerServices` carries a process starter that `EngineComposition` accepts and passes on, defaulting
      to the real one when none is given; existing callers compile unchanged.
- [ ] `PluginProtocolExtension.CreateHandler` and `McpClientExtension.CreateHandler` start their programs through the
      services' starter; tests in `Curl.Extended.Protocol.Plugin.UnitTests` and `Curl.Extended.Protocol.McpClient.UnitTests`
      show a stand-in starter given to `CreateHandler` receives the program name and arguments, and a Plugin test
      shows the helper search reads `PATH` through the host's environment reader, not the process environment.
- [ ] A test in `Curl.Engine.UnitTests/EngineCompositionTests.Extensions.cs` shows a starter passed to
      `EngineComposition` reaches the `ProtocolHandlerServices` an extension handler factory receives.
- [ ] The new ADR exists with "Decided by Claude under Stewart's delegation", and its line is in
      `Documentation/Planning/Decisions/README.md`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
- [ ] `Curl.Protocol.Abstractions`, `Curl.Engine`, Plugin and McpClient libraries stay at 100% line and branch coverage,
      complexity at most 10 and CRAP at most 30 per method; no package added; tests platform-neutral.

## Notes

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: ec78e582 c0680051 0dd67f5e 09011436 914c30cc e1d01e88.
