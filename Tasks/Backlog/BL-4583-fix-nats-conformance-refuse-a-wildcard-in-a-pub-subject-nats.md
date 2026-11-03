---
id: BL-4583
title: Fix Nats conformance: refuse a wildcard in a PUB subject (NATS-SUBJ-1)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Nats.UnitLibrary/NatsSubject.cs, Curl.Extended.Protocol.Nats.UnitLibrary/NatsProtocolHandler.cs, Curl.Extended.Protocol.Nats.UnitTests, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4583 — Fix Nats conformance: refuse a wildcard in a PUB subject (NATS-SUBJ-1)

## Goal

`curl -d hi nats://host/orders.*` (or a subject with a `>` token) is refused with exit 3 before curl connects, as the NATS client protocol forbids wildcards in a PUB subject; a subscribe keeps accepting wildcards.

## Context

Requirement Nats:NATS-SUBJ-1 (NATS client protocol, "Protocol conventions": wildcards are not allowed in PUB subjects). Today `NatsSubject.Of` refuses only whitespace and empty tokens, so curl publishes to `orders.*`. Found by BL-4487's case `NatsPublishCases.Publish_SubjectWithWildcard_IsRefusedBeforeConnecting` (`Curl.Conformance.Extended.UnitTests/Nats/`), which expects exit 3 and no `PUB` on the wire. The check applies to a publish and to `?request` (both send PUB), not to a subscribe (`NatsSubscribeCases.Subscribe_WildcardSubject_IsAllowed` must stay green).

## Acceptance criteria

- [ ] `NatsPublishCases.Publish_SubjectWithWildcard_IsRefusedBeforeConnecting` passes as a plain case (its `KnownFailures.txt` line for BL-4583 is removed).
- [ ] A focused unit test in `Curl.Extended.Protocol.Nats.UnitTests` pins the fix (ADR-0482).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests are green and `Curl.Extended.Protocol.Nats.UnitLibrary` stays at 100% line and branch coverage, complexity at most 10 (`Measure-CodeQuality.ps1`).

## Notes

## Log

- 2026-11-02: Created by BL-4487.