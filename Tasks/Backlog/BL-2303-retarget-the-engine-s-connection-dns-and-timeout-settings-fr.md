---
id: BL-2303
title: Retarget the engine's connection, DNS and timeout settings from CommandLineOptions to EasyNetworkOptions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2291, BL-2295]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Api.UnitLibrary/Options/Network, Curl.Api.UnitTests/Options/Network]
requirement: none
created: 2026-10-15
completed:
---
# BL-2303 — Retarget the engine's connection, DNS and timeout settings from CommandLineOptions to EasyNetworkOptions

## Goal

The engine reads its connection, DNS and timeout settings from `EasyNetworkOptions` instead of `CommandLineOptions`, and `Curl.Console` builds that record from the command line in a new `EasyOptionsFromCommandLine` class; behaviour is byte for byte unchanged.

## Context

ADR-0506, "The engine's input is a CLI-neutral option model"; plan phase 1 step 4. This is the first of seven retargets (BL-2303 to BL-2309), then BL-2310 removes the engine's `Curl.Cli` reference.

Rules for every retarget task:
- `Curl.Console/EasyOptionsFromCommandLine.cs` is the curl tool's `config2setopts.c`: it maps `CommandLineOptions` onto the easy option groups, one method per group. This task creates it; the others add their group. `Curl.Cli.UnitLibrary`'s `LibcurlSourceCode*` (the `--libcurl` output) already says which `CURLOPT_*` each command-line option sets: the mapping must agree with it, and where it does not, the command line's behaviour wins and the Notes say so.
- The engine classes that read the group's settings (here: `EngineComposition`'s transport, DNS resolver, TCP connector, local binding, Unix socket and connect-timeout factories, `TransferTimers`, `OptionGroupConnectionSettings`, `TransferContextFactory`'s connection fields) take the group record instead of `CommandLineOptions`. A setting the record lacks is added to the record in this task (its folder is in `touches`), with its `CURLOPT_*` doc and a default test.
- Tests that built `CommandLineOptions` only to drive an engine class now build the record; tests through the whole command line stay as they are. Add `EasyOptionsFromCommandLineTests` cases for every command-line option of the group (each option given, and its absence giving libcurl's default).
- `Curl.Engine.UnitLibrary` gains a reference to `Curl.Api.UnitLibrary` in this task.

## Acceptance criteria

- [ ] The engine classes named in Context take `EasyNetworkOptions`, not `CommandLineOptions`, for their network, DNS and timeout settings.
- [ ] `Curl.Console/EasyOptionsFromCommandLine.cs` maps every network-group command-line option (`--connect-to`, `--resolve`, `--interface`, `--local-port`, `--dns-*`, `--doh-url`, `-4`/`-6`, `--tcp-*`, `--keepalive-*`, `--unix-socket`, `--abstract-unix-socket`, `--connect-timeout`, `--happy-eyeballs-timeout-ms`, `--path-as-is`, `--proto*`, `--proto-default`, `--ssl`/`--ssl-reqd`, `--no-keepalive` and the rest of the group), each with a test.
- [ ] `dotnet build` is clean (warnings are errors).
- [ ] `dotnet test --filter "TestCategory!=Integration"` passes and no existing test was deleted, skipped or weakened.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case it lists still passes (the ratchet is unchanged).
- [ ] The gap office's scores are unchanged: `Curl.Console/CurlEasyErrorText.cs` stays at that path and `InProcessCurl.RunAsync`'s public signatures are unchanged.
- [ ] `Curl.Console/CLAUDE.md` describes `EasyOptionsFromCommandLine`; `--ai-help` is unaffected (no option changes).

## Notes

## Log

- 2026-10-15: Created.
