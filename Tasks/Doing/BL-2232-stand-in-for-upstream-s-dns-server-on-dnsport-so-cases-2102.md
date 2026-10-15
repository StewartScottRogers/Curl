---
id: BL-2232
title: Stand in for upstream's DNS server on %DNSPORT so cases 2102, 2103 and 2104 run
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2248]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-14
completed:
---
# BL-2232 — Stand in for upstream's DNS server on %DNSPORT so cases 2102, 2103 and 2104 run

## Goal

Stand in for upstream's `dns` server (`dnsd`) on `%DNSPORT`, so cases 2102, 2103 and 2104 (`CURL_DNS_SERVER=127.0.0.1:%DNSPORT`) run.

## Context

Source: Stewart's 2026-10-14 request to raise the gap office's behaviour score (aim 97-99%) by making the shared in-process harness run cases it leaves unmeasured; running `Curl.Conformance.UnitTests` on 2026-10-14 names these three for `%DNSPORT`. The gap tool uses `Curl.Conformance.UnitLibrary`.

Upstream's `dnsd` (`tests/dnsd.py` at curl-8_22_0, read it) answers DNS queries over UDP from the case's data; curl reaches it through `CURL_DNS_SERVER` (the c-ares / `--dns-servers` path). Find how Curl's own resolver handles `CURL_DNS_SERVER` and `--dns-servers` (search `Curl.Networking.UnitLibrary` and `Curl.Console` for `CURL_DNS_SERVER`); the conformance tests replace name lookup with `LoopbackOnlyDnsResolver`, so the stand-in must be reachable through `IDatagramConnector` (as `TftpServerConnector` is) by the DNS client path. If Curl's DNS-server option never reaches an injected datagram connector, say so in Notes and file the Curl-side change as its own task rather than widening this one. Pick a port not already used (see the library `CLAUDE.md`); register the stand-in in the runner chain.

## Acceptance criteria

- [ ] A DNS-over-UDP stand-in answers A and AAAA queries as `dnsd` does for the three cases; `DnsServerConnectorTests` pins query parsing, the answers and an unknown-name response.
- [ ] `%DNSPORT` has a value; screening lets `dns` cases run; cases 2102, 2103 and 2104 are no longer screened out. Each that passes is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; each that runs but fails is named in Notes as a real gap, with a follow-up task per distinct cause.
- [ ] Platform-neutral tests; `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary` shows no member outside a gate; `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` is green; `Curl.Conformance.UnitLibrary/CLAUDE.md` is updated.

## Notes

- 2026-10-14 (lane 2, prerequisite pass before any code): Curl's DNS-server option never reaches an injected datagram connector. No `.cs` file reads `CURL_DNS_SERVER`; `CurlComposition.CreateDnsResolver` builds `DnsServerResolver` only for `--dns-servers` & co., and it opens sockets through `IDnsSocketOpener`, not `IDatagramConnector`; and the conformance tests pass `LoopbackOnlyDnsResolver` to `CreateRunner`, which overrides any resolver. Filed the Curl-side change as BL-2248 and requeued this task behind it, as the Context asks.
- Also needed here once BL-2248 lands: the three cases need the `override-dns` feature, which `UpstreamCurlPlatform` keeps off (debug-only in curl). The harness should report it only to a case naming the `dns` server, as `PlatformFor` reports `Debug` only to httpsig cases (BL-2183), and turn on BL-2248's seam for those cases. This stays in this task's touches.

## Log

- 2026-10-14: Created.
- 2026-10-14: Backlog -> Doing.
- 2026-10-14: Doing -> Backlog. Waits on BL-2248: Curl never reads CURL_DNS_SERVER and its DNS-server resolver does not use the injected datagram connector
- 2026-10-14: Backlog -> Doing.
