---
id: BL-887
title: Pass --happy-eyeballs-timeout-ms into HttpRequestOptions.HappyEyeballsTimeout
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-644, BL-835]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-887 — Pass --happy-eyeballs-timeout-ms into HttpRequestOptions.HappyEyeballsTimeout

## Goal

`curl --http3 --happy-eyeballs-timeout-ms N https://...` races QUIC against TCP with an N ms timeout instead of the 200 ms default, because `Curl.Console/HttpRequestOptionsMapping.cs` copies the parsed option into `HttpRequestOptions.HappyEyeballsTimeout`.

## Context

- BL-835 added `HttpRequestOptions.HappyEyeballsTimeout` (default 200 ms, curl's default) and the race in `HttpProtocolHandler.RaceQuicAgainstTcpAsync` (ADR-0144 section 4, ADR-0172 as amended). Nothing fills it yet, so every transfer uses 200 ms.
- BL-644 parses `--happy-eyeballs-timeout-ms` into `CommandLineOptions` in `Curl.Cli.UnitLibrary`; use the member it adds. When the option is not given, leave the default.
- ADR-0144 measured `--http3 --happy-eyeballs-timeout-ms 1000` against a silent UDP peer connecting over TCP with `%{time_connect}` 1.007 s (curl.se's Windows build 8.18.0 with ngtcp2 1.21.0).

## Acceptance criteria

- [ ] `Curl.Console.UnitTests` has a test showing `HttpRequestOptionsMapping` maps `--happy-eyeballs-timeout-ms 1000` to `HappyEyeballsTimeout` of 1000 ms, and a test showing it stays 200 ms when the option is not given.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Console` reports 100% line and branch coverage and no failing member.

## Notes

Filed by BL-835 as its follow-up: `Curl.Console` was outside BL-835's `touches`.

## Log

- 2026-09-29: Created.
