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
completed: 2026-10-14
---
# BL-2232 — Stand in for upstream's DNS server on %DNSPORT so cases 2102, 2103 and 2104 run

## Goal

Stand in for upstream's `dns` server (`dnsd`) on `%DNSPORT`, so cases 2102, 2103 and 2104 (`CURL_DNS_SERVER=127.0.0.1:%DNSPORT`) run.

## Context

Source: Stewart's 2026-10-14 request to raise the gap office's behaviour score (aim 97-99%) by making the shared in-process harness run cases it leaves unmeasured; running `Curl.Conformance.UnitTests` on 2026-10-14 names these three for `%DNSPORT`. The gap tool uses `Curl.Conformance.UnitLibrary`.

Upstream's `dnsd` (`tests/dnsd.py` at curl-8_22_0, read it) answers DNS queries over UDP from the case's data; curl reaches it through `CURL_DNS_SERVER` (the c-ares / `--dns-servers` path). Find how Curl's own resolver handles `CURL_DNS_SERVER` and `--dns-servers` (search `Curl.Networking.UnitLibrary` and `Curl.Console` for `CURL_DNS_SERVER`); the conformance tests replace name lookup with `LoopbackOnlyDnsResolver`, so the stand-in must be reachable through `IDatagramConnector` (as `TftpServerConnector` is) by the DNS client path. If Curl's DNS-server option never reaches an injected datagram connector, say so in Notes and file the Curl-side change as its own task rather than widening this one. Pick a port not already used (see the library `CLAUDE.md`); register the stand-in in the runner chain.

## Acceptance criteria

- [x] A DNS-over-UDP stand-in answers A and AAAA queries as `dnsd` does for the three cases; `DnsServerConnectorTests` pins query parsing, the answers and an unknown-name response.
- [x] `%DNSPORT` has a value; screening lets `dns` cases run; cases 2102, 2103 and 2104 are no longer screened out. Each that passes is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; each that runs but fails is named in Notes as a real gap, with a follow-up task per distinct cause.
- [x] Platform-neutral tests; `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary` shows no member outside a gate; `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` is green; `Curl.Conformance.UnitLibrary/CLAUDE.md` is updated.

## Notes

- 2026-10-14 (lane 2, prerequisite pass before any code): Curl's DNS-server option never reaches an injected datagram connector. No `.cs` file reads `CURL_DNS_SERVER`; `CurlComposition.CreateDnsResolver` builds `DnsServerResolver` only for `--dns-servers` & co., and it opens sockets through `IDnsSocketOpener`, not `IDatagramConnector`; and the conformance tests pass `LoopbackOnlyDnsResolver` to `CreateRunner`, which overrides any resolver. Filed the Curl-side change as BL-2248 and requeued this task behind it, as the Context asks.
- Also needed here once BL-2248 lands: the three cases need the `override-dns` feature, which `UpstreamCurlPlatform` keeps off (debug-only in curl). The harness should report it only to a case naming the `dns` server, as `PlatformFor` reports `Debug` only to httpsig cases (BL-2183), and turn on BL-2248's seam for those cases. This stays in this task's touches.
- 2026-10-14 (lane 1): Read upstream's `tests/server/dnsd.c` (not `dnsd.py`, which is the http testenv's) and `runtests.pl`'s `<verify><dns>` at `curl-8_22_0`. Built `DnsServerConnector` / `DnsServerChannel` on port 9017 (choice: the next port no stand-in uses; servers.pm picks a free one), wrapping `TftpServerConnector` as the runner's datagram connector; `UpstreamCaseRun.DnsLog` and `UpstreamCaseVerification.CompareDns` (sorted lines, `host` attribute as a pattern filter, as runtests.pl). The conformance tests pass `honoursDnsServerVariable: true` and report `override-dns` only to a case naming the `dns` server. Choice: `HTTPS:`, `Delay-*` and `Rcode-*` dnsd lines are not emulated, as no vendored case uses them.
- Result: 2102 and 2103 pass and are listed. 2104 runs and fails on a real gap: Curl asks dnsd three times for a name that failed to resolve, where curl 8.22 caches the failure and asks once per type; filed as BL-2262.
- Measured: Measure-CodeQuality Curl.Conformance.UnitLibrary 100/100, 0 failing members; Curl.Conformance.UnitTests 3083 passed; full fast suite green.

## Log

- 2026-10-14: Created.
- 2026-10-14: Backlog -> Doing.
- 2026-10-14: Doing -> Backlog. Waits on BL-2248: Curl never reads CURL_DNS_SERVER and its DNS-server resolver does not use the injected datagram connector
- 2026-10-14: Backlog -> Doing.
- 2026-10-14: Doing -> Done. dnsd stand-in on %DNSPORT answers A/AAAA through the datagram connector; 2102 and 2103 pass, 2104's gap is BL-2262
