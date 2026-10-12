---
id: BL-2113
title: Close GF-0067: A name under .localhost goes to the system resolver and fails with exit 6, where curl resolves it to 127.0.0.1 and ::1
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-12
completed:
---
# BL-2113 — Close GF-0067: A name under .localhost goes to the system resolver and fails with exit 6, where curl resolves it to 127.0.0.1 and ::1

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0067 (A name under .localhost goes to the system resolver and fails with exit 6, where curl resolves it to 127.0.0.1 and ::1), so a later gap analysis measures each of `behaviour:test389` as `match`.

## Context

- Finding: GF-0067, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: Critical. Introduced in: not stated upstream.
- Items: `behaviour:test389`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

behaviour:test389 (-4 http://curlmachine.localhost:%HTTPPORT/389, a plain GET) expected 'reference curl exits 0; stdout 0 bytes: ', actual 'exit code 6, reference 0'; item evidence 'was match; in-process and out-of-process differ'. Curl's recorded stderr (C:\curl-crosscheck\recorded\389\curl\stderr.txt) is 'curl: (6) Could not resolve host: curlmachine.localhost'; the reference connected and received the 6-byte reply. Reproduce from the repository root, no server needed: & .\Curl.Console\bin\Release\net10.0\curl.exe -s -o NUL http://curlmachine.localhost:1/; $LASTEXITCODE  (Curl prints 6; the reference curl prints 7, connection refused on 127.0.0.1:1).

Suggestion, copied from the finding:

In Curl.Networking.UnitLibrary, change SystemDnsResolver.ResolveAsync (the production default resolver) to answer 'localhost' and every name ending in '.localhost' itself with ::1 and 127.0.0.1, without calling Dns.GetHostAddressesAsync. DnsServerResolver and DohDnsResolver already do this. Share one helper with TcpConnector.IsLocalhost. TcpConnector.ResolveNotingCacheAsync already keeps a localhost answer whole under -4/-6, so only the lookup is missing. Add SystemDnsResolverTests for 'curlmachine.localhost' and 'a.b.LOCALHOST' through the injectable lookUpHostAddresses delegate, asserting it is never called, and a TcpConnector test that -4 http://x.localhost/ dials 127.0.0.1. Upstream: tests/data/test389. ADR-0143 explains only part of this: it keeps localhost's answer whole under -4/-6 but does not make the system resolver answer .localhost names.

## Acceptance criteria

- [ ] `behaviour:test389`: Curl answers what curl 8.21.0 answers, `reference curl exits 0; stdout 0 bytes: `, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-12: Created.
