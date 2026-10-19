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
completed:
---
# BL-2820 — Keep whether Digest stayed marked available after a transfer's last 401 on the easy handle

## Goal

The easy handle remembers, from one transfer to the next, whether Digest stayed marked available after the transfer's last 401 (curl's `authhost.avail` keeping `CURLAUTH_DIGEST` because `authproblem` skipped `pickoneauth`), and passes it to the HTTP handler on the next transfer.

## Context

Split out of BL-2744 (upstream case 2030) so that task fits one run; BL-2744's Notes give curl's logic (lib/http.c `Curl_http_input_auth`, `Curl_http_auth_act`, `pickoneauth`; lib/vauth/digest.c `Curl_input_digest`). The bit is set when the transfer's last 401 offered Digest after the transfer had already held a Digest nonce (kept from an earlier transfer or received in this one) without `stale=true`.

Plumbing: `HttpProtocolHandler` reports the bit in `TransferReport`; `ManagedEasyTransfer` keeps it beside `keptDigestChallenges` (cleared by `Reset` and by the same origin rule); `TransferContextFactory` passes it as a new `HttpRequestOptions` property. This task only carries the bit; BL-2744 makes the handler act on it (resend the kept answer on the first 401).

## Acceptance criteria

- [ ] `TransferReport` has a property the HTTP handler sets when the transfer ended with Digest still marked available, with unit tests in `Curl.Protocol.Http.UnitTests` for set and not set (stale, first Digest challenge only).
- [ ] `HttpRequestOptions` has the matching property, filled by `TransferContextFactory` from `ManagedEasyTransfer`'s kept bit; an engine test beside `PerformAsync_EarlierTransferGotDigestChallenge_NextDigestTransferAnswersItFirst` shows it reaches the next transfer and that `Reset` clears it.
- [ ] `dotnet build` is clean, the fast tests pass, and ratchet cases 2024, 2025, 2029, 2031 and 2032 still pass.

## Notes

## Log

- 2026-10-19: Created.
