---
id: BL-2725
title: Make upstream case 678 pass: lib678's CURLOPT_CAINFO_BLOB transfer reaches the HTTPS stand-in
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-18
completed:
---
# BL-2725 — Make upstream case 678 pass: lib678's CURLOPT_CAINFO_BLOB transfer reaches the HTTPS stand-in

## Goal

Upstream case 678 passes through the runner and holds the ratchet.

## Context

Found by BL-2634, which made the runner run test678's `<precheck>` line `./libtest/libtests lib678 check`
through the ported `Lib678` (the precheck now passes quietly, so the case runs). The case then fails on
Windows with:

```
test678 fails: <verify><protocol> differs at byte 0 (line 1): expected "GET /678 HTTP/1.1\r\n", got the end
- 1: "GET /678 HTTP/1.1\r\n"
- 2: "Host: localhost:8989\r\n"
- 3: "User-Agent: CURLOPT_CAINFO_BLOB\r\n"
- 4: "Accept: */*\r\n"
- 5: "\r\n"
```

No request reaches the HTTPS stand-in (`https test-localhost.pem` on `%HTTPSPORT`), and the program's stdout
and stderr were both empty. Lib678 passes `%CERTDIR/certs/test-ca.crt` as `libtest_arg2`, reads it with
`LoadFile` and sets it as `CURLOPT_CAINFO_BLOB` with `CURLSSLOPT_REVOKE_BEST_EFFORT`. Find first whether the
file is read (an unread file ends with `OutOfMemory` before any transfer) or the TLS handshake against the
blob fails in the `Libcurl` engine (`Libcurl/Options/Tls/EasyTlsOptionSetters.cs` and the engine's TLS
trust), then add the project at fault to `touches` before any edit. Reproduce with `CURL_UPSTREAM_CASE=678`
and the `Manual` test of `Curl.Conformance.UnitTests` (see its `CLAUDE.md`). Check the case is
platform-neutral before holding it (Schannel on Windows, OpenSSL elsewhere; revocation best effort).

## Acceptance criteria

- [ ] test678 passes through the runner on Windows and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, or, if it still fails, its differing lines are filed with the board script.
- [ ] The fix adds a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 262ba952 c59fcab3 3d836d5e.
