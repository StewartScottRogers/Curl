---
id: BL-5391
title: Finish an HTTP/2 -I transfer after the final head when the HEAD response carries DATA
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/Http2StreamConnection.cs, Curl.Original.Protocol.Http.UnitTests]
requirement: none
created: 2026-11-17
completed:
---
# BL-5391 — Finish an HTTP/2 -I transfer after the final head when the HEAD response carries DATA

## Goal

`curl --http2-prior-knowledge -I` finishes with exit 0 after the final head when the HEAD response carries DATA, instead of failing with exit 16.

## Context

From the outside fitness review of 2026-11-17 (commit 9561bbe, `preview` build), finding STD-098 (Low, Confirmed by two reviewers; Windows h2c and TLS, Linux build; 3+ runs: needs a non-conformant server; the Windows PORT negotiates h2 by default over HTTPS). Area: HTTP/2 HEAD.

**Repro.** An HTTP/2 prior-knowledge server on 127.0.0.1:44101 that answers HEAD `/headbody` with HEADERS `:status 200`, `content-length: 5`, then DATA `hello` with END_STREAM.
- `curl -sS -m 15 --http2-prior-knowledge -I http://127.0.0.1:44101/headbody -w '[%{http_code}]\n'; echo $?`

**Expected (REF2).** `HTTP/2 200`, `content-length: 5`, blank line, `[200]`, empty stderr, exit 0. (With `-X HEAD` and no `-I`, both exit 16, which matches.)
**Actual (PORT).** The same head lines, then `curl: (16) nghttp2 shuts down connection with error 1: PROTOCOL_ERROR`, exit 16.
**Where to start.** `Curl.Original.Protocol.Http.UnitLibrary/Http2StreamConnection.cs` ~line 204 ("Reads the rest of the stream, dropping any DATA past the body"): with `-I` (no body wanted), finish after the final head.

## Acceptance criteria

- [ ] New tests in `Curl.Original.Protocol.Http.UnitTests` (a new file) prove that with `-I` semantics an HTTP/2 HEAD response whose stream carries DATA after the head completes with exit 0, the head lines reported and no error text.
- [ ] A test proves the `-X HEAD` (no `-I`) variant still fails with exit 16.
- [ ] A test proves a conformant HEAD response (HEADERS with END_STREAM) is unchanged.
- [ ] Tests are platform-neutral (or OS-conditioned where curl differs by platform).
- [ ] Hand check recorded in the Log: the command prints `[200]` and exits 0 with empty stderr; the `-X HEAD` variant still exits 16. Measure real curl with `Record-CurlExchange.ps1` where possible.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). The review's server was a Python `h2` one; replace it with hand-written frames in a C# file-based app, or the test's in-memory HTTP/2 peer.
- Review acceptance check: the command prints `[200]` and exits 0 with empty stderr; the `-X HEAD` variant still exits 16.

## Log

- 2026-11-17: Created.
