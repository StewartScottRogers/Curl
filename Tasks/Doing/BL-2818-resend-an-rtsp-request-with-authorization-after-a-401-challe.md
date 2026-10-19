---
id: BL-2818
title: Resend an RTSP request with Authorization after a 401 challenge under any-auth (from BL-2788's stash c61946edb9a51c86eef042dca7d5ca72bfc7d3ac)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2789]
touches: [Curl.Protocol.Rtsp.UnitLibrary, Curl.Protocol.Rtsp.UnitTests]
requirement: none
created: 2026-10-19
completed:
---
# BL-2818 — Resend an RTSP request with Authorization after a 401 challenge under any-auth (from BL-2788's stash c61946edb9a51c86eef042dca7d5ca72bfc7d3ac)

## Goal

An RTSP request with credentials under CURLAUTH_ANY that gets a 401 with WWW-Authenticate: Basic is resent once with the next CSeq and Authorization: Basic, as curl does (upstream case 3100).

## Context

Split from BL-2788 (cost cap). The work is done in stash c61946edb9a51c86eef042dca7d5ca72bfc7d3ac : `git diff c61946edb9a51c86eef042dca7d5ca72bfc7d3ac^1 c61946edb9a51c86eef042dca7d5ca72bfc7d3ac -- Curl.Protocol.Rtsp.UnitLibrary` (RtspReplyHead.Challenges, RtspReplyHeadParser, RtspProtocolHandler.AnswerChallenges, RtspReplyReader) and `git show c61946edb9a51c86eef042dca7d5ca72bfc7d3ac^3:Curl.Protocol.Rtsp.UnitTests/RtspProtocolHandlerAuthenticationRetryTests.cs` (7 tests passed). Apply those files only, then build, test and measure.

## Acceptance criteria

- [ ] RtspProtocolHandlerAuthenticationRetryTests in Curl.Protocol.Rtsp.UnitTests pass, showing a DESCRIBE sent without Authorization, then after a 401 Basic challenge resent with CSeq 2 and Authorization: Basic dXNlcjpwYXNzd29yZA==.
- [ ] Curl.Protocol.Rtsp.UnitLibrary stays at 100% line and branch coverage with no method past the complexity gate (Measure-CodeQuality.ps1 -Library Curl.Protocol.Rtsp.UnitLibrary).
- [ ] dotnet build is clean and the fast tests pass.

## Notes

## Log

- 2026-10-19: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: c39a9153 31329c52 5ea3895a.
