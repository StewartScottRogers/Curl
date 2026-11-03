---
id: BL-4609
title: Fix Stomp conformance: content-type on SEND
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Stomp.UnitLibrary/Client/StompClientFlow.cs, Curl.Extended.Protocol.Stomp.UnitTests, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4609 — Fix Stomp conformance: content-type on SEND

## Goal

Curl's `stomp://` SEND frame carries a `content-type` header whenever it has a body, as STOMP 1.2 says SEND frames
SHOULD ("SEND", "content-type"), so `Stomp:STOMP12-ContentType-1` passes.

## Context

Found by BL-4493's conformance cases: `StompSendCases.Publish_Body_SendsAContentType` in
`Curl.Conformance.Extended.UnitTests/Stomp/` is listed in `KnownFailures.txt` as `Stomp:STOMP12-ContentType-1 BL-4603`.
`StompClientFlow.SendFrame` writes only `destination` and `content-length`. Decide the type (a `-H "content-type: ..."`
given by the user, else a default such as `application/octet-stream`) and record it in an ADR.

## Acceptance criteria

- [ ] `StompSendCases.Publish_Body_SendsAContentType` passes and its `KnownFailures.txt` line is removed.
- [ ] A focused unit test in `Curl.Extended.Protocol.Stomp.UnitTests` pins the header (ADR-0482).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-02: Created.
