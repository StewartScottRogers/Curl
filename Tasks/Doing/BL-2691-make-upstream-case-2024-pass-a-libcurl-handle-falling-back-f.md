---
id: BL-2691
title: Make upstream case 2024 pass: a Libcurl handle falling back from Basic to Digest sends the Digest Authorization
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2747]
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2691 — Make upstream case 2024 pass: a Libcurl handle falling back from Basic to Digest sends the Digest Authorization

## Goal

Upstream case 2024 passes through the case runner and holds the ratchet.

## Context

Found by BL-2549, which ported lib2023 (libauthretry: request 100 with the wrong password under Basic, then request 200 with the right one under Digest on the same handle). Measured on Windows: `<verify><protocol> differs at byte 155 (line 8): expected "Authorization: Digest username="testuser", realm="testrealm", nonce="1", uri="/20240200", response="ed646c565f79e2dd9fa37cb5a621213c"", got "Accept: */*"` - Curl sends request 200 with no Authorization, where curl answers the Digest challenge the 401 of request 100 carried. The port matches upstream's C, so the difference is in Libcurl or the engine (how a handle keeps the last response's challenges when `CURLOPT_HTTPAUTH` changes between transfers). In the first pass find the project at fault and add it to `touches` before any edit. The Digest separator difference (BL-2666) may show next once the header is sent.

## Acceptance criteria

- [ ] Case 2024 passes through `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` and is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, or the difference left is filed as a task.
- [ ] The fix has a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-10-18 (lane 3, first pass, no code edited): the fault is not in the lib2023 port. `DigestAuthenticator` is stateless (ADR-0014) and nothing carries request 100's 401 Digest challenge into the handle's next transfer, where curl keeps it in `data->state.digest` (stored even while Digest is not allowed) and answers it on request 200. The fix spans Libcurl, Curl.Engine, Curl.Protocol.Abstractions and Curl.Protocol.Http (Engine and Abstractions are in BL-2734's touches, in Doing), so it is filed as BL-2747 and this task now only holds case 2024 on the ratchet once BL-2747 is Done. The Digest separator difference (BL-2666) may show next.

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 7dd24967 fd6bd9d4 3d836d5e.
- 2026-10-18: Doing -> Backlog. Waits on BL-2747: a Libcurl handle must keep the last 401's Digest challenge for its next transfer (Libcurl, Engine, Abstractions, Http; Engine and Abstractions overlap BL-2734 in Doing)
- 2026-10-18: Backlog -> Doing. Criteria at claim: 7dd24967 fd6bd9d4 3d836d5e.
