---
id: BL-2744
title: Make upstream case 2030 pass: the Digest retry after NTLM sends its Authorization header
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2747, BL-2820]
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Protocol.Abstractions.UnitLibrary/HttpRequestOptions.cs, Curl.Protocol.Abstractions.UnitLibrary/TransferReport.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs, Curl.Engine.UnitLibrary/TransferContextFactory.cs, Curl.Engine.UnitTests/ManagedCurlEngineTests.cs]
requirement: none
created: 2026-10-18
completed:
---
# BL-2744 â€” Make upstream case 2030 pass: the Digest retry after NTLM sends its Authorization header

## Goal

Upstream case 2030 (lib2023, libauthretry) passes through the case runner and holds the ratchet.

## Context

Found by BL-2689, which put 2025, 2029, 2031 and 2032 on the ratchet once BL-2635 fixed the NTLM type-1 flags. Measured on Windows with `CURL_UPSTREAM_CASE=2030`: `<verify><protocol> differs at byte 433 (line 13): expected "Authorization: Digest username=\"testuser\", realm=\"testrealm\", nonce=\"2\", uri=\"/20300200\", response=\"2f2d784ba53a0a307758a90e98d25c27\"\r\n", got "Accept: */*\r\n"` - the Digest request that should follow the changed auth scheme goes out without its Authorization header. Start with `Curl.Conformance.LibTest.UnitLibrary/Lib2023.cs` (how it switches `CURLOPT_HTTPAUTH` between requests) and the easy handle's auth state it drives; if the cause is in the HTTP auth engine, add that project to `touches`.

## Acceptance criteria

- [ ] `CURL_UPSTREAM_CASE=2030` with `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` passes, and 2030 is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] The fix has a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-10-18 (lane 5, first pass, no code edited): the fault is not in the lib2023 port. Request 100 (NTLM, wrong password) ends on a 401 that also offers `Digest realm="testrealm", nonce="2"`; curl stores that nonce in `data->state.digest` even while Digest is not allowed and answers it on request 200 under `CURLAUTH_DIGEST`. Curl's `DigestAuthenticator` is stateless and no hook carries the challenge into the handle's next transfer - exactly BL-2747 (the same cause as case 2024, BL-2691). Added BL-2747 to `depends-on` and requeued; once it is Done, rerun `CURL_UPSTREAM_CASE=2030` and hold 2030 on the ratchet. Note request 400 is expected twice with nonce 5 (curl treats the first Digest 401 as a fresh challenge, not a failure), which BL-2747's design must also produce.
- 2026-10-18 (lane 3, second claim, BL-2747 Done): request 200 now passes; 2030 failed only at request 400, sent once where curl sends it twice with nonce 5. Stashed HTTP-handler change: `HttpRequestPlan.AuthorizationAnswersKeptChallenges` marks a first request built from `KeptDigestChallenges`, and `ResendsKeptChallengesAnswer` resends that value unchanged on its first 401 that offers Digest; two tests in `HttpProtocolHandlerTests.KeptDigestChallenges.cs`. With it 2030 passes, but ratchet case 2024 goes red (request 400 sent twice where curl sends it once), so the resend needs one more bit of handle state.
- Why curl differs (lib/http.c, lib/vauth/digest.c): `Curl_http_input_auth` answers a Digest header with "Ignoring duplicate digest auth header" when `authhost.avail` already has `CURLAUTH_DIGEST`; `avail` is cleared only by `pickoneauth`, which `Curl_http_auth_act` skips once `authproblem` is set. A Digest header after a nonce is already held (not stale) fails `Curl_input_digest` (`authproblem`), but stores its nonce. In 2030 transfer 300 (NTLM) the second Digest 401 (nonce 5) sets `authproblem`, so `avail` keeps Digest into transfer 400: its first 401 is ignored as a duplicate and request 400 is resent with nonce 5; the second 401 fails it and keeps nonce 7 for request 500. In 2024 transfer 300 (Basic) its only 401 is the first Digest nonce, `pickoneauth` runs and clears `avail`, so request 400's 401 fails at once.
- What is left: the HTTP handler reports in `TransferReport` whether the transfer ended with Digest still marked available (its last 401 offered Digest after the transfer had already held a Digest nonce, kept or received, without `stale=true`); `ManagedEasyTransfer` keeps that bit beside `keptDigestChallenges` (cleared by `Reset` and with the origin rule) and `TransferContextFactory` passes it as an `HttpRequestOptions` property; `ResendsKeptChallengesAnswer` resends only when it is set. Then rerun 2030 and 2024, add 2030 to the ratchet (the stashed diff already lists it), and add an engine test beside `PerformAsync_EarlierTransferGotDigestChallenge_NextDigestTransferAnswersItFirst`.
- Widened `touches` for that: Curl.Protocol.Http (the resend), the two Abstractions files, and the Engine files. `Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs` is in BL-2694's `touches` (in Doing), so requeued until it is free.
- Widened `touches` for that: Curl.Protocol.Http (the resend), the two Abstractions files, and the Engine files. `Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs` is in BL-2694's `touches` (in Doing), so requeued until it is free.
- 2026-10-19 (lane 1, third claim, no code edited): the run started with under one dollar of its cost cap, too little for the five-project change. Split the handle-state plumbing (TransferReport, HttpRequestOptions, ManagedEasyTransfer, TransferContextFactory) into BL-2820 and added it to depends-on. What stays here: reapply stash 7eef9059 (HttpRequestPlan.AuthorizationAnswersKeptChallenges, ResendsKeptChallengesAnswer, its two tests, 2030 on the ratchet), make the resend conditional on BL-2820's new HttpRequestOptions property, rerun 2030 and 2024.

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: b96ecbf8 fd6bd9d4 3d836d5e.
- 2026-10-18: Doing -> Backlog. Waits on BL-2747: the handle must keep the last 401's Digest challenge for its next transfer
- 2026-10-18: Backlog -> Doing. Criteria at claim: b96ecbf8 fd6bd9d4 3d836d5e.
- 2026-10-18: Doing -> Backlog. Needs Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs, which BL-2694 (in Doing) touches: the handle must keep whether Digest stayed marked available after its last 401
- 2026-10-19: Backlog -> Doing. Criteria at claim: b96ecbf8 fd6bd9d4 3d836d5e.
- 2026-10-19: Doing -> Backlog. Waits on BL-2820: the easy handle must carry whether Digest stayed marked available into the next transfer
- 2026-10-19: Backlog -> Doing. Criteria at claim: b96ecbf8 fd6bd9d4 3d836d5e.
- 2026-10-19: Doing -> Blocked. Stewart: dark factory its runs of the last day already cost 3.16 of its 3.31 US dollar cost cap, so it was not run again; split the task; see Z:\repos\Curl.logs\BL-2744-*.jsonl
