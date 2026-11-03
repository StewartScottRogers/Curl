---
id: BL-4588
title: Fix Amqp1 conformance: Amqp1:Core-2.6.5-1 answer a peer's closing detach with detach
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-4528]
touches: [Curl.Extended.Protocol.Amqp1.UnitLibrary/Client/Amqp1ClientFlow.cs, Curl.Extended.Protocol.Amqp1.UnitTests, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4588 — Fix Amqp1 conformance: Amqp1:Core-2.6.5-1 answer a peer's closing detach with detach

## Goal

When the peer detaches curl's link with closed set (with or without an error), curl answers with its own closing detach before it ends the session and connection, and still reports the error with its exit code (Part 2 sections 2.6.5 and 2.6.6).

## Context

Found by BL-4528's AMQP 1.0 conformance cases against `Amqp1ReferenceContainer` (OASIS AMQP 1.0, Part 2). The client is `Curl.Extended.Protocol.Amqp1.UnitLibrary/Client/Amqp1ClientFlow.cs` (frames through `Amqp1FrameChannel`). Measure against the specification, not upstream curl. Case: `Amqp1SessionLinkCases.Link_DetachedByThePeer_AnswersWithADetach`: curl throws on the container's refusing detach (`ThrowIfPeerError`) and writes no detach.

## Acceptance criteria

- [x] `Amqp1SessionLinkCases.Link_DetachedByThePeer_AnswersWithADetach` passes and the `Amqp1:Core-2.6.5-1` line is removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`; the cases expecting exit 78 still pass.
- [x] A focused unit test in `Curl.Extended.Protocol.Amqp1.UnitTests` pins the answering detach (ADR-0482).
- [x] `Curl.Extended.Protocol.Amqp1.UnitLibrary` stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 (`Measure-CodeQuality.ps1`); the build is clean and the fast tests are green.

## Notes

- Decided: a detach the flow did not ask for is answered with a detach carrying the peer's own closed flag (Part 2 sections 2.6.5 and 2.6.6), in the new `AnswerUnaskedEndingAsync` beside the existing answer to an unasked close; the failure is then reported as before (exit 78 for the refused link). curl does not go on to send end and close after a failed link: the error ends the transfer, as an unasked close already does. Two unit tests pin it (with and without an error). Measure-CodeQuality for the library: 0 failing members; build clean; fast tests green on Windows, and the change is platform-neutral.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: caefe932 bab71660 a834f96f.
- 2026-11-02: Doing -> Done. Amqp1ClientFlow answers a peer's closing detach with its own detach before reporting the error
