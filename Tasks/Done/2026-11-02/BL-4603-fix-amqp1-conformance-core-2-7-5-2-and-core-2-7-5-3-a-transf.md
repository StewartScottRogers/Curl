---
id: BL-4603
title: Fix Amqp1 conformance: Core-2.7.5-2 and Core-2.7.5-3, a transfer on an unattached handle or with a changed delivery-id
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-4467]
touches: [Curl.Extended.Protocol.Amqp1.UnitLibrary/Client/Amqp1ClientFlow.cs, Curl.Extended.Protocol.Amqp1.UnitLibrary/Client/Amqp1ClientMessages.cs, Curl.Extended.Protocol.Amqp1.UnitTests/Client, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4603 — Fix Amqp1 conformance: Core-2.7.5-2 and Core-2.7.5-3, a transfer on an unattached handle or with a changed delivery-id

## Goal

Curl's `amqp1://` receiver rejects a transfer on a handle it never attached, ending the session with the error
`amqp:session:unattached-handle` and exiting 8, and fails with exit 8 on a continuation transfer whose delivery-id
differs from the first transfer's, instead of writing either message to the output.

## Context

OASIS AMQP 1.0 Part 2 section 2.7.5 (transfer) and 2.8.16 (session errors). Found by BL-4467's cases
`Amqp1ReceiveCases.Receive_TransferOnUnattachedHandle_EndsTheSessionWithUnattachedHandle` and
`Amqp1ReceiveCases.Receive_ContinuationChangesTheDeliveryId_FailsWithExit8` in
`Curl.Conformance.Extended.UnitTests/Amqp1/Messaging/`, matrix `Documentation/Conformance/Extended/Amqp1-Messaging.md`.
Today `Amqp1ClientFlow.ReceiveMessageAsync` ignores the transfer's handle and every continuation's delivery-id, so
both messages are written and curl exits 0. Fix: check each transfer's handle against the attached handle 0 (on a
mismatch send `end` carrying error `amqp:session:unattached-handle`, then fail with `CurlExitCode.WeirdServerReply`)
and each continuation's delivery-id (null or equal to the first's; otherwise fail with `WeirdServerReply`).

## Acceptance criteria

- [x] Both cases above pass, and their lines `Amqp1:Core-2.7.5-2 BL-4602` and `Amqp1:Core-2.7.5-3 BL-4602` are removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`.
- [x] Focused unit tests in `Curl.Extended.Protocol.Amqp1.UnitTests` pin both fixes (ADR-0482).
- [x] `Curl.Extended.Protocol.Amqp1.UnitLibrary` stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 (`Measure-CodeQuality.ps1`).
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- Delivered directly (a two-check fix in one method; no architect stage needed). `Amqp1ClientFlow.ReceiveTransferAsync` checks every transfer's handle against the attached handle 0: on a mismatch it sends `end` carrying error `amqp:session:unattached-handle` and fails with WeirdServerReply. A continuation whose delivery-id is present and differs from the first transfer's fails with WeirdServerReply; a null one is allowed (Part 2 section 2.7.5).
- Message texts (no real curl speaks AMQP 1.0, so nothing to measure; default taken in the style of the existing `Unexpected` message): "Weird server reply: AMQP 1.0 transfer on unattached handle <h>" and "Weird server reply: AMQP 1.0 continuation transfer changed delivery-id <a> to <b>".
- Unit tests in `Amqp1ClientFlowTests` (unattached handle, changed delivery-id, omitted delivery-id); `Amqp1ServerBytes.Transfer` takes handle and delivery-id. Measure-CodeQuality: 0 failing members. Full build clean (-warnaserror), fast tests green; change is platform-neutral (no paths, OS text or certificates).

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: c967bfea 4cc8864f 3862b628 914c30cc.
- 2026-11-02: Doing -> Done. A receiver ends the session with amqp:session:unattached-handle on a transfer to an unattached handle and fails with exit 8 on a continuation with a changed delivery-id
