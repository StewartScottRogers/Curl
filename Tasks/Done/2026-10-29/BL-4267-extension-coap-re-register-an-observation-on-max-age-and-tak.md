---
id: BL-4267
title: Extension CoAP: re-register an observation on Max-Age and take block-wise notifications
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4248]
touches: [Curl.Extension.Coap.UnitLibrary/Observe, Curl.Extension.Coap.UnitTests/Observe, Curl.Extension.Coap.UnitLibrary/CLAUDE.md, Documentation/Planning/Decisions/ADR-1064-coap-observe-writes-each-notification-then-a-newline.md]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4267 — Extension CoAP: re-register an observation on Max-Age and take block-wise notifications

## Goal

Under `--coap-observe`, Curl re-registers when the last notification's Max-Age runs out without a fresh one (RFC 7641 section 3.3.1), and takes a notification larger than one block by fetching its remaining blocks (RFC 7959 section 2.6), writing it once, whole.

## Context

Follows BL-4248, whose ADR-1064 left these two parts of RFC 7641 to this task. `Curl.Extension.Coap.Observe.CoapObservation` follows notifications; `Curl.Extension.Coap.Blockwise.CoapBlockwiseTransfer` fetches Block2 responses. Extension rules: ADR-0999; tested against the RFCs, not curl; held to the four quality gates.

## Acceptance criteria

- [x] A unit test over a fake `IDatagramConnector` and a fake `TimeProvider` shows a GET with Observe 0 and the same token sent again once the last notification's Max-Age (default 60 s) plus a margin passes without a notification (RFC 7641 section 3.3.1).
- [x] A unit test shows a notification carrying Block2 with M set written once, whole, after its later blocks are fetched without Observe (RFC 7959 section 2.6).
- [x] ADR-1064 is updated to describe both behaviours.
- [x] `Curl.Extension.Coap.UnitLibrary` meets the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Delivered directly in `CoapObservation` (plan small enough that the full /feature pipeline would have cost more than the change); `CoapProtocolHandler` and `CoapBlockwiseTransfer` are unchanged.
- Touches widened to ADR-1064 (an acceptance criterion names it) and the library's `CLAUDE.md` (its `CoapObservation` bullet must stay true); no other task in Doing on origin/work/dark-factory names either.
- Decisions (recorded in ADR-1064): margin 5 s past Max-Age; re-registration non-confirmable with the registration's token and options under the next Message ID, repeated each time the wait passes; later blocks fetched by reusing `CoapBlockwiseTransfer` with its first exchange answered by the notification itself, under a token of their own (last bit flipped) so notifications are not taken as block responses; sequence order judged on the first block; deregistration takes the next unused Message ID.
- Tests: `ExecuteAsync_NoNotificationWithinMaxAgeAndTheMargin_ReregistersWithObserveZeroAndTheSameToken` (default and Max-Age 10, over `ImmediateTimeProvider`) and `ExecuteAsync_ANotificationWithMoreBlocks_FetchesTheLaterBlocksWithoutObserveAndWritesItOnceWhole`, plus `BlockFetchTokenOf` and `MaxAgeOf` tests. Measure-CodeQuality -Library Curl.Extension.Coap.UnitLibrary: 0 failing members. Fast tests: 70 projects passed, none failed. No path, file system or OS-dependent output, so the change is platform-neutral for CI on Linux and macOS.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 76fa3855 8bb8989a 2ee4459d bd9d4ca0 3d836d5e.
- 2026-10-29: Doing -> Done. --coap-observe re-registers after Max-Age plus 5 s and writes block-wise notifications whole
