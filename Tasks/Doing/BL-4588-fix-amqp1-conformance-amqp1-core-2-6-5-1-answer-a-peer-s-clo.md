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
completed:
---
# BL-4588 — Fix Amqp1 conformance: Amqp1:Core-2.6.5-1 answer a peer's closing detach with detach

## Goal

When the peer detaches curl's link with closed set (with or without an error), curl answers with its own closing detach before it ends the session and connection, and still reports the error with its exit code (Part 2 sections 2.6.5 and 2.6.6).

## Context

Found by BL-4528's AMQP 1.0 conformance cases against `Amqp1ReferenceContainer` (OASIS AMQP 1.0, Part 2). The client is `Curl.Extended.Protocol.Amqp1.UnitLibrary/Client/Amqp1ClientFlow.cs` (frames through `Amqp1FrameChannel`). Measure against the specification, not upstream curl. Case: `Amqp1SessionLinkCases.Link_DetachedByThePeer_AnswersWithADetach`: curl throws on the container's refusing detach (`ThrowIfPeerError`) and writes no detach.

## Acceptance criteria

- [ ] `Amqp1SessionLinkCases.Link_DetachedByThePeer_AnswersWithADetach` passes and the `Amqp1:Core-2.6.5-1` line is removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`; the cases expecting exit 78 still pass.
- [ ] A focused unit test in `Curl.Extended.Protocol.Amqp1.UnitTests` pins the answering detach (ADR-0482).
- [ ] `Curl.Extended.Protocol.Amqp1.UnitLibrary` stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 (`Measure-CodeQuality.ps1`); the build is clean and the fast tests are green.

## Notes

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: caefe932 bab71660 a834f96f.
