---
id: BL-2745
title: Make upstream Digest libtest case 2027 pass: the second request of each round sends no Authorization header
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl, Libcurl.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Engine.UnitLibrary/Curl.Engine.UnitLibrary.csproj, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-18
completed: 2026-10-18
---
# BL-2745 — Make upstream Digest libtest case 2027 pass: the second request of each round sends no Authorization header

## Goal

Upstream case 2027 (lib2023, libauthretry over Digest) passes through the case runner and holds the ratchet.

## Context

Found by BL-2690, which put 1985, 2026 and 2028 on the ratchet once BL-2666 fixed the Digest field separator. Measured on Windows on 2026-10-18 with `CURL_UPSTREAM_CASE=2027` and `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust`:

```
test2027 fails: <verify><protocol> differs at byte 304 (line 12): expected "Authorization: Digest username=\"testuser\", realm=\"testrealm\", nonce=\"2\", uri=\"/20270200\", response=\"785ca3ef511999f7e9c178195f5b388c\"\r\n", got "Accept: */*\r\n"
```

The first Digest request (line 7) now matches; the request for `/20270200` goes out with no `Authorization` header where curl sends Digest with the server's new nonce `2`. Start from how Libcurl keeps or drops Digest state between performs on one easy handle when `CURLOPT_HTTPAUTH` changes (lib2023 switches the auth scheme between rounds), and compare with 2026 and 2028, which pass. A focused unit test in `Libcurl.UnitTests` goes with the fix (ADR-0482); add `Libcurl.UnitTests` to `touches` when claiming if no live task names it.

## Acceptance criteria

- [x] `CURL_UPSTREAM_CASE=2027` passes `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust`, and 2027 is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [x] A unit test in `Libcurl.UnitTests` pins the header the fix restores.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-10-18: Cause: after BL-2709 the request for `/20270200` already matched; the remaining gap was request 400 of lib2023's second round. libcurl 8.22.0 keeps `authhost.avail` on the handle from the refused 401 that ended request 300, so request 400's first 401 Digest header is ignored as a duplicate and curl resends the same nonce-5 answer once before the next 401 ends it. Fix in `HttpProtocolHandler` (`RepeatsKeptDigestAnswer`, `HttpRequestPlan.AuthorizationAnswersKeptDigestChallenges`), only when the kept picked scheme is Digest, since after a Basic perform `pickoneauth` cleared `avail` (upstream 2024, which the first cut broke). ADR-0700.
- 2026-10-18: Added Curl.Protocol.Http.UnitLibrary and Curl.Protocol.Http.UnitTests (the fix lands in the HTTP handler, not Libcurl), Libcurl.UnitTests (the pinned test), and Curl.Engine.UnitLibrary/Curl.Engine.UnitLibrary.csproj (`InternalsVisibleTo Libcurl.UnitTests`, so the Libcurl test runs a handle through the managed engine over the Engine tests' linked `ScriptedConnector` with no socket) to touches. No task in Doing on origin/work/dark-factory named them (BL-2748 names two Engine .cs files, BL-2788 Rtsp).
- 2026-10-18: Measured on Windows: 2023 to 2028, 694 and 1985 pass; `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary`: 0 failing members. Not written: curl's `Ignoring duplicate digest auth header.` verbose line (no case checks it).

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 8c0688eb 3b60059b 3d836d5e.
- 2026-10-18: Doing -> Done. Upstream case 2027 passes and holds the ratchet: a refused kept Digest answer is sent once more after a perform that picked Digest (ADR-0700)
