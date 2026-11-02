---
id: BL-4508
title: Run the injected run clock through the QUIC dialer, the CURL_DNS_SERVER resolver and the security context factory
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Console/CurlComposition.cs, Curl.Engine.UnitLibrary/EngineComposition.Transports.cs, Curl.Engine.UnitLibrary/EngineComposition.Dns.cs, Curl.Engine.UnitLibrary/EngineComposition.Authentication.cs, Curl.Console/InProcessCurl.cs, Curl.Console.UnitTests/CurlCompositionDialingClockTests.cs]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4508 — Run the injected run clock through the QUIC dialer, the CURL_DNS_SERVER resolver and the security context factory

## Goal

A run given a clock through `InProcessCurl.RunAsync(..., TimeProvider)` or `CurlComposition.CreateRunner(timeProvider:)` runs every one of its timers on that clock, as the parameter's doc comment says.

## Context

Found by the shift-end review (ADR-0710) of the shift merged at e3af7deb5, by the silent-failure-hunter reviewer. BL-4500 added `timeProvider` to `CurlComposition.CreateRunner` (`Curl.Console/CurlComposition.cs:367`) and a public `InProcessCurl.RunAsync` overload, and documents it as "the clock every timer of the run runs on". Three factories the dialing runner calls still hard-code `TimeProvider.System`:

- the QUIC/HTTP/3 dialer: `CreateDatagramConnectorQuicDialer`, `Curl.Engine.UnitLibrary/EngineComposition.Transports.cs:282`;
- the `CURL_DNS_SERVER` resolver: `DnsServerVariableResolverOf`, `Curl.Engine.UnitLibrary/EngineComposition.Dns.cs:273`;
- the Kerberos/NTLM security context factory (`CreateDialingSecurityContextFactory`): the KDC socket transport, the SRV resolver, the KDC proxy transport and `HandBuiltSecurityContextFactory`, `Curl.Engine.UnitLibrary/EngineComposition.Authentication.cs:102-113`.

A test that advances a fake clock with `--http3`, `CURL_DNS_SERVER` or `--negotiate` therefore still waits on real time for those timers while `-m` fires on the fake clock: a timeout order curl never produces, with nothing saying the clock was ignored. The doc comment is false as it stands ("say what it does, do what it says").

## Acceptance criteria

- [x] The dialing runner passes its run clock to the QUIC dialer, the `CURL_DNS_SERVER` resolver and the security context factory; none of the three uses `TimeProvider.System` on the dialing path when a clock is given.
- [x] Without a clock, each still runs on `TimeProvider.System` (production unchanged).
- [x] A unit test shows at least one of the three timing out on an advanced fake clock without waiting in real time.
- [x] The `timeProvider` doc comments in `CurlComposition.cs` and `InProcessCurl.cs` name only timers that really use the clock.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Added `Curl.Console/InProcessCurl.cs` (a criterion names its doc comment) and the new test file `Curl.Console.UnitTests/CurlCompositionDialingClockTests.cs` to `touches`; no task in Doing on work/dark-factory held either.
- `CreateDatagramConnectorQuicDialer`, `DnsServerVariableResolverOf` and `CreateDialingSecurityContextFactory` (and `CreateSecurityContextFactory` beneath it) each take an optional `TimeProvider? timeProvider = null` that falls back to `TimeProvider.System`, so callers without a clock (the executable, `ManagedCurlEngine`) are unchanged. `CurlComposition.CreateRunner` passes its run clock to all three; the engine's own dialing path (`DialersOf`) now passes its clock to the QUIC dialer too.
- Test: `DnsServerVariableResolverOf_WithAClock_TimesOutASilentServerOnThatClock` advances a fake clock through the resolver's 2, 4 and 8 second attempts and finishes in about 0.1 s, where the real clock would take 14 s.
- CI on Linux and macOS: the change is platform-neutral (no paths, OS errors or certificates), and the fast tests are green here (114 test projects passed). Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary,Curl.Console reported 0 failing members.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 2e2d1f5f dd834adf 883e7975 a2ae2cb0 3d836d5e.
- 2026-11-02: Doing -> Done. The dialing runner's injected clock now drives the QUIC dialer, the CURL_DNS_SERVER resolver and the security context factory
