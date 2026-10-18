---
id: BL-2744
title: Make upstream Digest libtest case 2027 pass: the second request of each round sends no Authorization header
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-18
completed:
---
# BL-2744 — Make upstream Digest libtest case 2027 pass: the second request of each round sends no Authorization header

## Goal

Upstream case 2027 (lib2023, libauthretry over Digest) passes through the case runner and holds the ratchet.

## Context

Found by BL-2690, which put 1985, 2026 and 2028 on the ratchet once BL-2666 fixed the Digest field separator. Measured on Windows on 2026-10-18 with `CURL_UPSTREAM_CASE=2027` and `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust`:

```
test2027 fails: <verify><protocol> differs at byte 304 (line 12): expected "Authorization: Digest username=\"testuser\", realm=\"testrealm\", nonce=\"2\", uri=\"/20270200\", response=\"785ca3ef511999f7e9c178195f5b388c\"\r\n", got "Accept: */*\r\n"
```

The first Digest request (line 7) now matches; the request for `/20270200` goes out with no `Authorization` header where curl sends Digest with the server's new nonce `2`. Start from how Libcurl keeps or drops Digest state between performs on one easy handle when `CURLOPT_HTTPAUTH` changes (lib2023 switches the auth scheme between rounds), and compare with 2026 and 2028, which pass. A focused unit test in `Libcurl.UnitTests` goes with the fix (ADR-0482); add `Libcurl.UnitTests` to `touches` when claiming if no live task names it.

## Acceptance criteria

- [ ] `CURL_UPSTREAM_CASE=2027` passes `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust`, and 2027 is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] A unit test in `Libcurl.UnitTests` pins the header the fix restores.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-18: Created.
