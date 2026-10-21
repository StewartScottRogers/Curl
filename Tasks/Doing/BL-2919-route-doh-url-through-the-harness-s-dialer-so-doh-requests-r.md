---
id: BL-2919
title: Route --doh-url through the harness's dialer so DoH requests reach the in-memory servers
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary/EngineComposition.cs, Curl.Engine.UnitTests/EngineCompositionDohDialingTests.cs]
requirement: none
created: 2026-10-21
completed:
---
# BL-2919 — Route --doh-url through the harness's dialer so DoH requests reach the in-memory servers

## Goal

Under the conformance harness's `EngineDialing`, a `--doh-url` command resolves through a `DohDnsResolver` whose DoH connections go through `dialing.TcpDialer` (the in-memory servers), so upstream cases 2100 and 2117 send their DoH POSTs to the harness's `sws`.

## Context

Found by BL-2837 (2026-10-21). `EngineComposition.CreateTransports` takes `ResolverOf(dialing, ...)`: with `dialing` given it returns `dialing.DnsResolver` and never calls `CreateDnsResolver`, so `EasyNetworkOptions.DohUrl` is ignored under the harness. Test 2100 (`https://foo.example.com:%HTTPSPORT/2100 --doh-url https://%HOSTIP:%HTTPSPORT/21000001 --doh-insecure`) then ends `curl: (6) Could not resolve host: foo.example.com` with no request recorded. Fix: when `network.DohUrl` is set and `dialing` is given, build `CreateDohResolver(dohUrl, <TcpConnector over dialing.TcpDialer with dialing.DnsResolver for the DoH host and the DoH TLS provider>, ...)` in place of `dialing.DnsResolver`; production (no dialing) is unchanged. Never a real socket: the harness's ratchet opens none.

## Acceptance criteria

- [x] With an `EngineDialing` and a `--doh-url`, `CreateTransports`' resolver is a `DohDnsResolver` whose connections go through `dialing.TcpDialer`; a unit test in `Curl.Engine.UnitTests/EngineCompositionDohDialingTests.cs` pins it with a fake dialer that records the DoH POST.
- [x] Without `--doh-url` the resolver is still `dialing.DnsResolver` (unit test).
- [x] `$env:CURL_UPSTREAM_CASE='2100'` was run and its result recorded in Notes: skipped for the `Debug` feature before reaching the DoH POSTs; the rest is BL-2928.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and Curl.Engine.UnitLibrary keeps 100% line and branch coverage.

## Notes

- `EngineComposition.CreateTransports` now builds the resolver through `DialingResolverOf` when `dialing` is given: with `--doh-url` a `DohDnsResolver` over `CreateDohConnector(..., dialing.TcpDialer, dialing.DnsResolver)` (new optional `hostResolver` parameter, default `SystemDnsResolver`), else `dialing.DnsResolver`. Production unchanged. Tests: `EngineCompositionDohDialingTests`.
- Criterion 3 not checkable: `CURL_UPSTREAM_CASE=2100` ends `test2100 is skipped: Curl lacks the feature Debug` before running, so the runner never reaches the DoH POSTs. Lifting that skip is outside this task's `touches`; the unit test pins the POST reaching the dialer instead.
- Follow-up: BL-2928 (lift the Debug skip for the named-case run).
- `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary`: 0 failing members.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 1b6f541b 37a362b9 e53a05cf 9963d6cb.
