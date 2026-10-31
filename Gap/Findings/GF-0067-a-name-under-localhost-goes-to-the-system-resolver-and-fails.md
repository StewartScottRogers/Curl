---
id: GF-0067
title: A name under .localhost goes to the system resolver and fails with exit 6, where curl resolves it to 127.0.0.1 and ::1
area: behaviour
platform: windows
key: behaviour:localhost-subdomain-not-resolved-to-loopback
severity: Critical
status: closed
scope: target
introduced-in:
opened: 2026-10-12_0951
closed: 2026-10-12_2042
regression: false
items: [behaviour:test389]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
task: BL-2113
tasks: [BL-2113]
---
# GF-0067 - A name under .localhost goes to the system resolver and fails with exit 6, where curl resolves it to 127.0.0.1 and ::1

## Summary

Curl differs from upstream curl in behaviour: A name under .localhost goes to the system resolver and fails with exit 6, where curl resolves it to 127.0.0.1 and ::1.

## Evidence

behaviour:test389 (-4 http://curlmachine.localhost:%HTTPPORT/389, a plain GET) expected 'reference curl exits 0; stdout 0 bytes: ', actual 'exit code 6, reference 0'; item evidence 'was match; in-process and out-of-process differ'. Curl's recorded stderr (C:\curl-crosscheck\recorded\389\curl\stderr.txt) is 'curl: (6) Could not resolve host: curlmachine.localhost'; the reference connected and received the 6-byte reply. Reproduce from the repository root, no server needed: & .\Curl.Console\bin\Release\net10.0\curl.exe -s -o NUL http://curlmachine.localhost:1/; $LASTEXITCODE  (Curl prints 6; the reference curl prints 7, connection refused on 127.0.0.1:1).

## Suggestion

In Curl.Networking.UnitLibrary, change SystemDnsResolver.ResolveAsync (the production default resolver) to answer 'localhost' and every name ending in '.localhost' itself with ::1 and 127.0.0.1, without calling Dns.GetHostAddressesAsync. DnsServerResolver and DohDnsResolver already do this. Share one helper with TcpConnector.IsLocalhost. TcpConnector.ResolveNotingCacheAsync already keeps a localhost answer whole under -4/-6, so only the lookup is missing. Add SystemDnsResolverTests for 'curlmachine.localhost' and 'a.b.LOCALHOST' through the injectable lookUpHostAddresses delegate, asserting it is never called, and a TcpConnector test that -4 http://x.localhost/ dials 127.0.0.1. Upstream: tests/data/test389. ADR-0143 explains only part of this: it keeps localhost's answer whole under -4/-6 but does not make the system resolver answer .localhost names.

## Measurements

- 2026-10-12_0951: 1 of 1 items are gaps.
- 2026-10-12_2042: 0 of 1 items are gaps.
- 2026-10-13_1406: 0 of 1 items are gaps.
- 2026-10-14_0904: 0 of 1 items are gaps.
- 2026-10-15_0304: 0 of 1 items are gaps.
- 2026-10-15_0543: 0 of 1 items are gaps.
- 2026-10-15_0751: 0 of 1 items are gaps.
- 2026-10-15_1657: 0 of 1 items are gaps.
- 2026-10-17_2225: 0 of 1 items are gaps.
- 2026-10-19_0804: 0 of 1 items are gaps.
- 2026-10-19_1329: 0 of 1 items are gaps.
- 2026-10-22_0148: 0 of 1 items are gaps.
- 2026-10-23_1637: 0 of 1 items are gaps.
- 2026-10-25_1542: 0 of 1 items are gaps.
- 2026-10-30_2103: 0 of 1 items are gaps.

## Log

- 2026-10-12_0951: Opened by gap-behaviour.
- 2026-10-12_1039: Filed BL-2113.
- 2026-10-12_2042: Closed: run 2026-10-12_2042 measured every item as match or excluded.
