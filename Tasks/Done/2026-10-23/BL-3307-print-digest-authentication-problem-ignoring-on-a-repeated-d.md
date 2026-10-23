---
id: BL-3307
title: Print 'Digest authentication problem, ignoring.' on a repeated Digest challenge under --anyauth
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/HttpAuthProblemLines.cs, Curl.Protocol.Http.UnitTests/HttpAuthProblemLinesTests.cs]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3307 — Print 'Digest authentication problem, ignoring.' on a repeated Digest challenge under --anyauth

## Goal

Under --anyauth, a second 401 repeating a Digest challenge with no stale=true prints "* Digest authentication problem, ignoring." before its Digest header line, as curl 8.22.0 does.

## Context

Found by BL-3282 (differential case auth-all-challenges-v, -sv --anyauth -u u:p against /authall: 401 with Negotiate, NTLM, Digest realm="r", nonce="abc", qop="auth" and Basic challenges). Real curl, after picking Negotiate and retrying on a fresh connection, prints on the second 401 "* Digest authentication problem, ignoring." between "< WWW-Authenticate: NTLM" and the Digest header: libcurl's digest state still holds the first nonce, so Curl_input_digest refuses the repeat. Curl prints no such line. Start at HttpAuthProblemLines (Curl.Protocol.Http.UnitLibrary). Reproduce with Record-CurlExchange.ps1 -Connections 3.

## Acceptance criteria

- [x] Curl's -sv --anyauth exchange against the four-challenge 401 prints the Digest problem line where real curl does, pinned by a unit test in Curl.Protocol.Http.UnitTests.
- [x] The fast tests pass and Measure-CodeQuality.ps1 reports 0 failing members for each changed library.

## Notes

- 2026-10-23 lane-8 run. libcurl feeds every Digest challenge to Curl_input_digest, which refuses a non-stale one while the transfer's Digest state holds an earlier nonce. HttpAuthProblemLines now takes that state as held when a Digest answer was sent, or any answer under several allowed schemes (--anyauth probes first, so an earlier 401 was read). Kept inside the touched file rather than a cross-head flag in HttpProtocolHandler; recorded in ADR-0793 (decided under delegation).
- Pinned by HttpAuthProblemLinesTests.LinesBefore_DigestHeaderUnderAnyAuth_GivesTheProblemLineOnceAnAnswerWasSent. Checked end to end with Record-CurlExchange.ps1 -Connections 3 against Curl.Console's Debug build: the second 401 prints the line between WWW-Authenticate: NTLM and the Digest header, where BL-3282 measured curl 8.22.0 printing it. Real curl was not re-run; BL-3282's measurement was used.
- dotnet build: 0 errors. Fast tests: 44 test projects passed, 0 failed. Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary: 0 failing members. The change is text only and platform-neutral, so CI on Linux and macOS is not at risk from it.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: e3ec11d1 69edf767.
- 2026-10-23: Doing -> Done. Under --anyauth the second 401's repeated Digest challenge prints 'Digest authentication problem, ignoring.' as curl does
