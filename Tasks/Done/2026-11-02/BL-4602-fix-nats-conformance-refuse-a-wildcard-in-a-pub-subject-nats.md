---
id: BL-4602
title: Fix Nats conformance: refuse a wildcard in a PUB subject (NATS-SUBJ-1)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Nats.UnitLibrary/NatsSubject.cs, Curl.Extended.Protocol.Nats.UnitLibrary/NatsProtocolHandler.cs, Curl.Extended.Protocol.Nats.UnitTests, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4602 — Fix Nats conformance: refuse a wildcard in a PUB subject (NATS-SUBJ-1)

## Goal

`curl -d hi nats://host/orders.*` (or a subject with a `>` token) is refused with exit 3 before curl connects, as the NATS client protocol forbids wildcards in a PUB subject; a subscribe keeps accepting wildcards.

## Context

Requirement Nats:NATS-SUBJ-1 (NATS client protocol, "Protocol conventions": wildcards are not allowed in PUB subjects). Today `NatsSubject.Of` refuses only whitespace and empty tokens, so curl publishes to `orders.*`. Found by BL-4487's case `NatsPublishCases.Publish_SubjectWithWildcard_IsRefusedBeforeConnecting` (`Curl.Conformance.Extended.UnitTests/Nats/`), which expects exit 3 and no `PUB` on the wire. The check applies to a publish and to `?request` (both send PUB), not to a subscribe (`NatsSubscribeCases.Subscribe_WildcardSubject_IsAllowed` must stay green).

## Acceptance criteria

- [x] `NatsPublishCases.Publish_SubjectWithWildcard_IsRefusedBeforeConnecting` passes as a plain case (its `KnownFailures.txt` line for BL-4583 is removed).
- [x] A focused unit test in `Curl.Extended.Protocol.Nats.UnitTests` pins the fix (ADR-0482).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests are green and `Curl.Extended.Protocol.Nats.UnitLibrary` stays at 100% line and branch coverage, complexity at most 10 (`Measure-CodeQuality.ps1`).

## Notes

- `NatsSubject.RefuseWildcardToPublish` refuses a subject holding a whole-token wildcard (`*` or `>`) with exit 3; `NatsProtocolHandler` calls it for a publish and for `?request` (both send PUB) before connecting, never for a subscribe. A `*` or `>` inside a longer token (`a*b`) is not a wildcard in the NATS client protocol, so it stays allowed. That is the spec's rule, not a choice, so there is no ADR.
- Also removed the two `Grpc:Http2-*-1 BL-4591` lines from `KnownFailures.txt`: BL-4595 (a934afbf8) made those cases pass, which left the fast tests red on the shared branch. The file is in this task's touches.
- Checks: `dotnet build Curl.slnx -warnaserror` is clean. Fast tests are green (Nats 74, Conformance.Extended 763). `Measure-CodeQuality.ps1 -Library Curl.Extended.Protocol.Nats.UnitLibrary` reports 0 failing members. The changes are platform-neutral.

## Log

- 2026-11-02: Created by BL-4487.
- 2026-11-02: Backlog -> Doing. Criteria at claim: a26bc883 3e135a93 c3396a1b.
- 2026-11-02: Doing -> Done. A publish or request to a NATS subject with a * or > token is refused with exit 3 before connecting; subscribe still accepts wildcards
