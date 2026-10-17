---
id: BL-2590
title: Libcurl: end a transfer whose read callback answers CURL_READFUNC_ABORT with CURLE_ABORTED_BY_CALLBACK (test513)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2590 — Libcurl: end a transfer whose read callback answers CURL_READFUNC_ABORT with CURLE_ABORTED_BY_CALLBACK (test513)

## Goal

A Libcurl transfer whose read callback answers `CURL_READFUNC_ABORT` ends with `CURLE_ABORTED_BY_CALLBACK` (42), so upstream test513 passes.

## Context

Found by BL-2528 (lib513 port). test513 posts with `CURLOPT_POSTFIELDSIZE` 1 and a read callback that returns `CURL_READFUNC_ABORT`; curl 8.22.0 answers 42, Curl answers 52 (`CURLE_GOT_NOTHING`): `test513 fails: <verify><errorcode>: expected exit code 42, got 52`. Run it with `CURL_UPSTREAM_CASE=<n>` and the `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` test in `Curl.Conformance.UnitTests`.

## Acceptance criteria

- [ ] A focused unit test in `Curl.Engine.UnitTests` shows a read callback answering `CURL_READFUNC_ABORT` ends the transfer with `CURLE_ABORTED_BY_CALLBACK`.
- [ ] test513 passes through the runner on Windows and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Measure-CodeQuality.ps1` reports 0 failing members for the changed library.

## Notes

- Cause: `ManagedEasyTransfer` read `CURLOPT_READFUNCTION` only under `CURLOPT_UPLOAD`, so a `CURLOPT_POST` with no `CURLOPT_POSTFIELDS` sent an empty body and never called the read callback; test513 then got 52. Fix (left uncommitted in the lane worktree, stashed by the shift): `ReadCallbackPostBodyOf` gives such a post a `StreamBody` over the read callback (`CURLOPT_POSTFIELDSIZE` as its length, chunked at -1, `application/x-www-form-urlencoded`) as `OwnInputsOf`'s `FormBody`, and `CallbackStreamsOf` adds its stream to the perform's callback reads, so `CURL_READFUNC_ABORT` ends it with `CURLE_ABORTED_BY_CALLBACK`. Focused tests in `ManagedEasyTransferTests.Callbacks.cs` (abort; body sent, sized by POSTFIELDSIZE; chunked when unknown; no body otherwise). `ManagedEasyTransferTests` 202/202 green; `CURL_UPSTREAM_CASE=513` passes through the runner on Windows (HTTP only, no platform-dependent output).
- 2026-10-17: added `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` to `touches` (the ratchet criterion needs it); BL-2595, in Doing, touches it too, so requeued. Left for the next claim: apply the stash, add 513 to the ratchet, run the fast tests and `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary`.

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: e69776de 20427775 4272a6b9 c84701f0.
- 2026-10-17: Doing -> Backlog. Needs Curl.Conformance.UnitTests/PassingUpstreamCases.txt, which BL-2595 (in Doing) touches; engine fix and tests are done in the stash
- 2026-10-17: Backlog -> Doing. Criteria at claim: e69776de 20427775 4272a6b9 c84701f0.
