---
id: BL-4352
title: Extension: Oblivious HTTP relay POST without the transfer's own headers
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-4137]
touches: [Curl.Extension.Abstractions.UnitLibrary/Transfer/TransferContextRequests.cs, Curl.Extension.Abstractions.UnitLibrary/Transfer/RetargetedTransferContext.cs, Curl.Extension.Abstractions.UnitTests, Curl.Extension.ObliviousHttp.UnitLibrary/Relay/HookRequest.cs, Curl.Extension.ObliviousHttp.UnitTests/Relay]
requirement: none
created: 2026-10-30
completed:
---
# BL-4352 — Extension: Oblivious HTTP relay POST without the transfer's own headers

## Goal

With --ohttp-relay, the POST to the relay and the GET of the key configuration carry only the headers the hook names, never the transfer's own -H, -A, -b or --user, which travel only inside the encapsulated request.

## Context

ADR-1149 left this out: TransferContextRequests.WithRequest (BL-4303) replaces the URL, method and body but keeps curl's own header options, so the relay sees them, which defeats RFC 9458 section 2's separation. Add a hook API in Curl.Extension.Abstractions that sends a request without the transfer's own header options and credentials, and use it in HookRequest.SendAsync.

## Acceptance criteria

- [x] Curl.Extension.Abstractions gives a hook a request with no -H, -A, -e, -b or --user of the transfer's, with unit tests.
- [x] HookRequest.SendAsync uses it, and an ObliviousHttpTransferHookTests test shows a -H line reaching only the encapsulated request.
- [x] ADR-1149's Left out section no longer lists it.
- [x] The changed libraries meet the four quality gates by Measure-CodeQuality.ps1, dotnet build is clean and the fast tests pass.

## Notes

- Relay and key fetches use `WithoutOwnHeaders` (also clears `-b` strings via SendsCookieStrings=false; stored cookies matching the relay host still go). The ADR is ADR-1157 (the task named ADR-1149 in error); its Left out entry is replaced. Quality run: 0 failing members.

## Log

- 2026-10-30: Created.
- 2026-10-30: Backlog -> Doing. Criteria at claim: 2f5e1687 40f512f9 37551924 9e24dc71.
