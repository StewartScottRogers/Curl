---
id: BL-889
title: Pass --happy-eyeballs-timeout-ms into HttpRequestOptions.HappyEyeballsTimeout
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-644, BL-835]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-29
completed: 2026-09-30
---
# BL-889 — Pass --happy-eyeballs-timeout-ms into HttpRequestOptions.HappyEyeballsTimeout

## Goal

`curl --http3 --happy-eyeballs-timeout-ms N https://...` races QUIC against TCP with an N ms timeout instead of the 200 ms default, because `Curl.Console/HttpRequestOptionsMapping.cs` copies the parsed option into `HttpRequestOptions.HappyEyeballsTimeout`; and every TCP connect races IPv4 against IPv6 with that timeout, because `CurlComposition.CreateTcpConnector` passes it to `TcpConnector`'s `happyEyeballsTimeout`.

## Context

- BL-835 added `HttpRequestOptions.HappyEyeballsTimeout` (default 200 ms, curl's default) and the race in `HttpProtocolHandler.RaceQuicAgainstTcpAsync` (ADR-0144 section 4, ADR-0172 as amended). Nothing fills it yet, so every transfer uses 200 ms.
- BL-644 parsed `--happy-eyeballs-timeout-ms` into `CommandLineOptions.HappyEyeballsTimeout` (`TimeSpan?`, `null` when not given) and gave `TcpConnector` a `happyEyeballsTimeout` constructor argument (`null` for 200 ms; exposed as `TcpConnector.HappyEyeballsTimeout`), raced by `AddressFamilyRace` (ADR-0254). `Curl.Console` was outside BL-644's `touches`, so passing it to the connector moved here. When the option is not given, leave the default.
- ADR-0144 measured `--http3 --happy-eyeballs-timeout-ms 1000` against a silent UDP peer connecting over TCP with `%{time_connect}` 1.007 s (curl.se's Windows build 8.18.0 with ngtcp2 1.21.0).

## Acceptance criteria

- [x] `Curl.Console.UnitTests` has a test showing `HttpRequestOptionsMapping` maps `--happy-eyeballs-timeout-ms 1000` to `HappyEyeballsTimeout` of 1000 ms, and a test showing it stays 200 ms when the option is not given.
- [x] `Curl.Console.UnitTests` has a test showing `CurlComposition.CreateTcpConnector` gives a connector whose `HappyEyeballsTimeout` is 50 ms for `--happy-eyeballs-timeout-ms 50` and `TcpConnector.DefaultHappyEyeballsTimeout` without it.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Console` reports 100% line and branch coverage and no failing member.

## Notes

Filed by BL-835 as its follow-up: `Curl.Console` was outside BL-835's `touches`.

- 2026-09-30: `HttpRequestOptionsMapping.HappyEyeballsTimeoutOf` maps the option and holds it to the longest .NET timer delay (uint.MaxValue - 1 ms), as `TcpConnector` does, because `HttpProtocolHandler` passes it to `Task.Delay`, which throws past that; only a 64-bit C long (Linux, macOS) reaches it, so that test is excluded on Windows. `CreateTcpConnector` passes `options.HappyEyeballsTimeout` straight through (`TcpConnector` clamps it). Tests: `HappyEyeballsTimeoutMappingTests`. Curl.Console 100% line and branch, 0 failing members.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. --happy-eyeballs-timeout-ms now sets both the QUIC-vs-TCP race (HttpRequestOptions) and the IPv4-vs-IPv6 race (TcpConnector)
