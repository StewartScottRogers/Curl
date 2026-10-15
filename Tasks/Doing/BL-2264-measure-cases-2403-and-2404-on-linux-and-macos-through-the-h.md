---
id: BL-2264
title: Measure cases 2403 and 2404 on Linux and macOS through the %HTTP2TLSPORT stand-in and list or fix them
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2245]
touches: [Curl.Conformance.UnitTests, Curl.Conformance.Http2Server.UnitLibrary, Curl.Conformance.Http2Server.UnitTests]
requirement: none
created: 2026-10-14
completed:
---
# BL-2264 — Measure cases 2403 and 2404 on Linux and macOS through the %HTTP2TLSPORT stand-in and list or fix them

## Goal

test2403 is on PassingUpstreamCases.txt or has a follow-up task per cause, and test2404’s remaining skip reason is recorded.

## Context

BL-2245 (2026-10-14) put nghttpx’s TLS front (ALPN h2, X-Forwarded-Proto: https) on %HTTP2TLSPORT = 9018. On Windows 2403 and 2404 skip for http/2, which Curl’s Schannel-matching build lacks (ADR-0495), and the lane machine has no Linux .NET, so they were never measured. The ratchet reports an unlisted case that passes as Inconclusive (passes and can be listed): read that message for test2403 and test2404 in the Linux and macOS CI runs’ test logs (or run CURL_UPSTREAM_CASE=2403 with the Manual category on Linux). test2404 also names <tool>lib2404</tool> and is expected to skip for it.

## Acceptance criteria

- [ ] test2403’s result on Linux and macOS is recorded in Notes; it is in Curl.Conformance.UnitTests/PassingUpstreamCases.txt if it passes, otherwise each distinct cause has a follow-up task (or is fixed here in the HTTP/2 stand-in with a focused unit test).
- [ ] test2404’s skip reason on Linux is recorded in Notes, with a follow-up task if it is not the <tool>.
- [ ] The fast tests are green.

## Notes

## Log

- 2026-10-14: Created.
- 2026-10-14: Backlog -> Doing.
