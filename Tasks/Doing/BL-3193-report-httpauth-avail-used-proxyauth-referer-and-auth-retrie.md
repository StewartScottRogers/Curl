---
id: BL-3193
title: Report HTTPAUTH_AVAIL/USED, PROXYAUTH_*, REFERER and auth retries in REDIRECT_COUNT
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/TransferInfoFromReport.cs, Curl.Engine.UnitTests/TransferInfoAuthAndRefererTests.cs, Libcurl.UnitTests/Info/EasyInfoAuthAndRefererTests.cs]
requirement: none
created: 2026-10-22
completed:
---
# BL-3193 — Report HTTPAUTH_AVAIL/USED, PROXYAUTH_*, REFERER and auth retries in REDIRECT_COUNT

## Goal

`CURLINFO_HTTPAUTH_AVAIL`, `HTTPAUTH_USED`, `PROXYAUTH_AVAIL`, `PROXYAUTH_USED`, `REFERER` and `REDIRECT_COUNT` report what upstream reports.

## Context

Stewart reported on 2026-10-22 (Libcurl / curl_easy API conformance batch, report 23 (auth, referer, redirects)). From report 23: HTTPAUTH_AVAIL and HTTPAUTH_USED are always 0; they should give the methods the server offered and the one used (basic, digest, and the rest). PROXYAUTH_* the same for a proxy. CURLINFO_REFERER is null even after CURLOPT_REFERER is set. REDIRECT_COUNT should count the authentication retry (digest, CURLAUTH_ANY), as upstream does. The CLI's `-w` variables that read the same values should pick the fix up.

Measure upstream libcurl 8.22.0 behaviour before pinning values (a small C or libtest-style program, or the matching curl CLI option through `Record-CurlExchange.ps1`); pin each platform's answer in its own `[OSCondition]` test where they differ. Tests use fakes or loopback only.

## Acceptance criteria

- [ ] Tests show HTTPAUTH_AVAIL/USED after a 401 offering Basic and Digest with CURLAUTH_ANY, and after plain Basic, match upstream's bitmasks.
- [ ] Tests show PROXYAUTH_AVAIL/USED after a 407 the same way.
- [ ] A test shows REFERER returns the value set with CURLOPT_REFERER (and the auto-referer value after a redirect with AUTOREFERER).
- [ ] A test shows REDIRECT_COUNT after a digest auth retry equals upstream's value.
- [ ] Build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 0 failing members.

## Notes

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 8442c290 2677bb42 ed48196a de830169 3639a5e6.
