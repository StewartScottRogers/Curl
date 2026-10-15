---
id: BL-2262
title: Cache a failed name resolution across a command's URLs as curl 8.22 does, so test2104 passes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-14
completed: 2026-10-14
---
# BL-2262 — Cache a failed name resolution across a command's URLs as curl 8.22 does, so test2104 passes

## Goal

A name that fails to resolve is cached for the rest of the command, as curl 8.22.0 caches a failed resolve, so later URLs naming the same host fail with exit 6 without asking the DNS server again, and upstream test2104 passes.

## Context

Found by BL-2232, which stood in for upstream's `dnsd` on `%DNSPORT`. test2104 (`http://examplehost.invalid/2104` three times, `CURL_DNS_SERVER` pointing at a dnsd answering no records) expects exactly one A and one AAAA query in `dnsd.input`; Curl asks three times each (`<verify><dns> differs ... got QNAME examplehost.invalid QTYPE A` three times). curl 8.22.0 keeps a negative entry in its DNS cache (see `lib/hostip.c` at `curl-8_22_0`, the negative-entry handling of `Curl_resolv` / the DNS cache) for the cache timeout. Curl's cache is `Curl.Networking.UnitLibrary/DnsCache.cs`. Measure real curl's behaviour for a failed name across URLs in one command (and `--dns-cache-timeout 0`) before pinning it.

## Acceptance criteria

- [x] A failed resolve is cached as curl caches it, pinned by a focused test in `Curl.Networking.UnitTests` (ADR-0482).
- [x] test2104 passes and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` shows no member outside a gate.

## Notes

- Measured curl 8.22.0 (Schannel, threaded resolver) on 2026-10-14: `curl -v http://nohost.invalid/a http://nohost.invalid/b` looks the name up again for the second URL (no `Negative DNS entry`), and `--dns-cache-timeout` is unknown to that build. test2104 runs the c-ares path (`CURL_DNS_SERVER`), which caches the failure.
- Decision (ADR-0499): `TcpConnector` caches a look-up that answered no address as a negative entry only when its resolver is `DnsServerResolver`, exit 43 (`BadConfiguration`) aside; the system resolver and DoH keep nothing. The later connect writes the existing negative-entry lines and exits 6.
- Tests: `ConnectAsync_AfterADnsServerFoundNoSuchName_FailsFromANegativeDnsEntryWithoutAskingTheServerAgain` and `ConnectAsync_AfterADnsServerConfigurationThatDidNotParse_AsksAgainWithoutANegativeDnsEntry` in `TcpConnectorTests.DnsCache.cs`.
- Measure-CodeQuality for Curl.Networking.UnitLibrary: 100% line and branch; `ResolveNotingCacheAsync` reached complexity 12, so the caching moved into `CacheLookUp`; rebuilt and retested after.

## Log

- 2026-10-14: Created.
- 2026-10-14: Backlog -> Doing.
- 2026-10-14: Doing -> Done. A failed resolve through the DNS server resolver is cached as a negative entry, so test2104 passes
