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
completed:
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

- [ ] Both cases above pass, and their lines `Amqp1:Core-2.7.5-2 BL-4602` and `Amqp1:Core-2.7.5-3 BL-4602` are removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`.
- [ ] Focused unit tests in `Curl.Extended.Protocol.Amqp1.UnitTests` pin both fixes (ADR-0482).
- [ ] `Curl.Extended.Protocol.Amqp1.UnitLibrary` stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 (`Measure-CodeQuality.ps1`).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-11-02: Created.
