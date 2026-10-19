---
id: BL-2820
title: Keep whether Digest stayed marked available after a transfer's last 401 on the easy handle
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Protocol.Abstractions.UnitLibrary/HttpRequestOptions.cs, Curl.Protocol.Abstractions.UnitLibrary/TransferReport.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs, Curl.Engine.UnitLibrary/TransferContextFactory.cs, Curl.Engine.UnitTests/ManagedCurlEngineTests.cs]
requirement: none
created: 2026-10-19
completed: 2026-10-19
---
# BL-2820 — Keep whether Digest stayed marked available after a transfer's last 401 on the easy handle

## Goal

The easy handle remembers, from one transfer to the next, whether Digest stayed marked available after the transfer's last 401 (curl's `authhost.avail` keeping `CURLAUTH_DIGEST` because `authproblem` skipped `pickoneauth`), and passes it to the HTTP handler on the next transfer.

## Context

Split out of BL-2744 (upstream case 2030) so that task fits one run; BL-2744's Notes give curl's logic (lib/http.c `Curl_http_input_auth`, `Curl_http_auth_act`, `pickoneauth`; lib/vauth/digest.c `Curl_input_digest`). The bit is set when the transfer's last 401 offered Digest after the transfer had already held a Digest nonce (kept from an earlier transfer or received in this one) without `stale=true`.

Plumbing: `HttpProtocolHandler` reports the bit in `TransferReport`; `ManagedEasyTransfer` keeps it beside `keptDigestChallenges` (cleared by `Reset` and by the same origin rule); `TransferContextFactory` passes it as a new `HttpRequestOptions` property. This task only carries the bit; BL-2744 makes the handler act on it (resend the kept answer on the first 401).

## Acceptance criteria

- [x] `TransferReport` has a property the HTTP handler sets when the transfer ended with Digest still marked available, with unit tests in `Curl.Protocol.Http.UnitTests` for set and not set (stale, first Digest challenge only).
- [x] `HttpRequestOptions` has the matching property, filled by `TransferContextFactory` from `ManagedEasyTransfer`'s kept bit; an engine test beside `PerformAsync_EarlierTransferGotDigestChallenge_NextDigestTransferAnswersItFirst` shows it reaches the next transfer and that `Reset` clears it.
- [x] `dotnet build` is clean, the fast tests pass, and ratchet cases 2024, 2025, 2029, 2031 and 2032 still pass.

## Notes

- 2026-10-19 (lane 1): `TransferReport.DigestStaysAvailable` and `HttpRequestOptions.DigestStaysAvailable` added. The HTTP handler's `HttpRequestPlan.HoldsDigestNonce` starts true when `KeptDigestChallenges` offer Digest and turns true on each retry answering a 401 that offers Digest (`HoldingDigestNonceAfter`); the report sets the bit when the final response is a 401 offering Digest without `stale=true` while the plan held a nonce (`DigestStaysAvailableAfter`). `ManagedEasyTransfer` keeps it in `keptDigestStaysAvailable` (cleared by `Reset`, false after an abort) and passes it only to the same origin (`KeptDigestStaysAvailableFor`) through `TransferContextFactory.KeptDigestStaysAvailable`.
- Choice: only a final 401 sets the bit (a transfer ending 2xx leaves it unset), as `ChallengesOfLast401` keeps challenges only from a final 401; BL-2744 can widen it if case 2030 needs more.
- Tests: `ExecuteAsync_LastResponseAfterKeptDigestNonce_ReportsWhetherDigestStaysAvailable` (5 rows), `ExecuteAsync_SecondDigest401OfOneTransfer_ReportsDigestStaysAvailable`, `PerformAsync_EarlierTransferEndedWithDigestStillAvailable_NextTransferCarriesItUntilReset`. Build clean, fast tests green, Conformance ratchet green (2024, 2025, 2029, 2031, 2032 on it). Measure-CodeQuality on the three libraries: 0 failing members.

## Log

- 2026-10-19: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: 8fbcdf02 b8264556 4b2caed5.
- 2026-10-19: Doing -> Done. The easy handle carries whether Digest stayed marked available after a transfer's last 401 into its next transfer's HTTP options
