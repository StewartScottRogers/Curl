---
id: BL-2644
title: Clear the last transfer's info on curl_easy_reset so upstream cases 1532 and 1534 pass
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2539]
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2644 — Clear the last transfer's info on curl_easy_reset so upstream cases 1532 and 1534 pass

## Goal

After `curl_easy_reset`, `CURLINFO_RESPONSE_CODE` is 0 and `CURLINFO_FILETIME` is -1 again, as upstream's `Curl_initinfo` leaves them, so upstream cases 1532 and 1534 pass.

## Context

Found by BL-2539 (ports `Lib1532` and `Lib1534` in `Curl.Conformance.LibTest.UnitLibrary`). Through the case runner on Windows, test1532 exits 22 with `Lib1532.cs:NN curl_easy_reset failed to zero the response code` / `possible regression of github bug 1017` on stderr, and test1534 exits 2 with `Lib1534.cs:NN filetime init failed; expected -1 but is 30`: after a transfer that answered 200 with `Last-Modified` 30 s past the epoch, `ManagedEasyTransfer.Reset()` (`Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs`) clears the options and headers but keeps the last transfer's `TransferInfo`. Upstream's `curl_easy_reset` calls `Curl_initinfo`, which zeroes the response code and sets the file time to -1 (curl 8.22.0 `lib/easy.c`, `lib/getinfo.c`). A `curl_easy_duphandle` copy already starts with fresh info.

## Acceptance criteria

- [ ] A unit test in `Curl.Engine.UnitTests` shows that after a transfer and `Reset()`, the transfer's `Info.ResponseCode` is 0 and `Info.FileTime` is -1.
- [ ] Upstream cases 1532 and 1534 pass through the runner on Windows and are added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` (neither prints a path, OS error text or certificate).
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reports 0 failing members.

## Notes

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 2ee28948 e9830330 4272a6b9 c4aff572.
