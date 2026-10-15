---
id: BL-2303
title: Map the command line's connection, DNS and timeout options onto EasyNetworkOptions in EasyOptionsFromCommandLine
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2291, BL-2295]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Api.UnitLibrary/Options/Network, Curl.Api.UnitTests/Options/Network]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2303 — Map the command line's connection, DNS and timeout options onto EasyNetworkOptions in EasyOptionsFromCommandLine

## Goal

`Curl.Console` builds `EasyNetworkOptions` from the command line in a new `EasyOptionsFromCommandLine` class, ready for the engine to read in place of `CommandLineOptions` (BL-2323); behaviour is byte for byte unchanged.

## Context

ADR-0506, "The engine's input is a CLI-neutral option model"; plan phase 1 step 4. This is the first of seven retargets (BL-2303 to BL-2309), then BL-2310 removes the engine's `Curl.Cli` reference.

Rules for every retarget task:
- `Curl.Console/EasyOptionsFromCommandLine.cs` is the curl tool's `config2setopts.c`: it maps `CommandLineOptions` onto the easy option groups, one method per group. This task creates it; the others add their group. `Curl.Cli.UnitLibrary`'s `LibcurlSourceCode*` (the `--libcurl` output) already says which `CURLOPT_*` each command-line option sets: the mapping must agree with it, and where it does not, the command line's behaviour wins and the Notes say so.
- The engine classes that read the group's settings (here: `EngineComposition`'s transport, DNS resolver, TCP connector, local binding, Unix socket and connect-timeout factories, `TransferTimers`, `OptionGroupConnectionSettings`, `TransferContextFactory`'s connection fields) take the group record instead of `CommandLineOptions`. A setting the record lacks is added to the record in this task (its folder is in `touches`), with its `CURLOPT_*` doc and a default test.
- Tests that built `CommandLineOptions` only to drive an engine class now build the record; tests through the whole command line stay as they are. Add `EasyOptionsFromCommandLineTests` cases for every command-line option of the group (each option given, and its absence giving libcurl's default).
- `Curl.Engine.UnitLibrary` gains a reference to `Curl.Api.UnitLibrary` in this task.

## Acceptance criteria

- [x] ~~The engine classes named in Context take `EasyNetworkOptions`~~ — split out to BL-2323 (see Notes).
- [x] `Curl.Console/EasyOptionsFromCommandLine.cs` maps every network-group command-line option (`--connect-to`, `--resolve`, `--interface`, `--local-port`, `--dns-*`, `--doh-url`, `-4`/`-6`, `--tcp-*`, `--keepalive-*`, `--unix-socket`, `--abstract-unix-socket`, `--connect-timeout`, `--happy-eyeballs-timeout-ms`, `--path-as-is`, `--proto*`, `--proto-default`, `--ssl`/`--ssl-reqd`, `--no-keepalive` and the rest of the group), each with a test.
- [x] `dotnet build` is clean (warnings are errors).
- [x] `dotnet test --filter "TestCategory!=Integration"` passes and no existing test was deleted, skipped or weakened.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case it lists still passes (the ratchet is unchanged).
- [x] The gap office's scores are unchanged: `Curl.Console/CurlEasyErrorText.cs` stays at that path and `InProcessCurl.RunAsync`'s public signatures are unchanged.
- [x] `Curl.Console/CLAUDE.md` describes `EasyOptionsFromCommandLine`; `--ai-help` is unaffected (no option changes).

## Notes

- 2026-10-15, split (rule 4, before any code): the engine retarget is about 95 `CreateTransports` and 25 `CreateTcpConnector` call sites across `Curl.Console.UnitTests`, `Curl.Engine.UnitTests` and `Curl.Engine.IntegrationTests`, plus a Curl.Console + Curl.Engine quality run; with the mapper it does not fit one run. This task now delivers the mapper, the record's gaps and ADR-0513; BL-2323 (depends on this) retargets the engine factories, and BL-2310 now also depends on BL-2323. `Curl.Engine.UnitLibrary`'s reference to `Curl.Api.UnitLibrary` moved to BL-2323 with the retarget that needs it.
- Decided (ADR-0513, by Claude under Stewart's delegation): `EasyNetworkOptions.ConnectTimeout` defaults to zero (libcurl's stored value, taken as 300 s) so "not given" stays distinguishable for the `[CONNECTTIMEOUT]` trace timer; `--ip-tos`, `--vlan-priority` and `--mptcp` become record members (`IpTypeOfService`, `VlanPriority`, `MultipathTcp`), as curl sets them through socket callbacks rather than an option.
- Where the mapping disagrees with `--libcurl`'s `LibcurlSourceCode*` and the command line's behaviour wins: `--ftp-ssl-control` outranks `--ssl-reqd` (`--libcurl` writes `CURLUSESSL_ALL`); `--proto`/`--proto-redir` keep `smb`/`smbs` (dropped by `--libcurl`); `--proto-default` is lowercase (`--libcurl` writes it as typed). Keepalive timings map only with keepalive on and only when given, as `--libcurl` writes them.
- `Curl.Console.csproj` now references `Curl.Api.UnitLibrary`.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Console,Curl.Api.UnitLibrary` - 100% line and branch; one member past the gate (`WithSockets`, complexity 14), split into `WithKeepAlive` and `WithSockets` (6 and 3). Fast tests: all green (Console 2332 passed, Api 421, Conformance 3112).

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. EasyOptionsFromCommandLine.NetworkOf maps every network-group option onto EasyNetworkOptions (ADR-0513); engine retarget split to BL-2323
