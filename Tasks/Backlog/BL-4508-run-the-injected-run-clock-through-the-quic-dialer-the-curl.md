---
id: BL-4508
title: Run the injected run clock through the QUIC dialer, the CURL_DNS_SERVER resolver and the security context factory
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Console/CurlComposition.cs, Curl.Engine.UnitLibrary/EngineComposition.Transports.cs, Curl.Engine.UnitLibrary/EngineComposition.Dns.cs, Curl.Engine.UnitLibrary/EngineComposition.Authentication.cs]
requirement: none
created: 2026-11-02
completed:
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

- [ ] The dialing runner passes its run clock to the QUIC dialer, the `CURL_DNS_SERVER` resolver and the security context factory; none of the three uses `TimeProvider.System` on the dialing path when a clock is given.
- [ ] Without a clock, each still runs on `TimeProvider.System` (production unchanged).
- [ ] A unit test shows at least one of the three timing out on an advanced fake clock without waiting in real time.
- [ ] The `timeProvider` doc comments in `CurlComposition.cs` and `InProcessCurl.cs` name only timers that really use the clock.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-02: Created.
