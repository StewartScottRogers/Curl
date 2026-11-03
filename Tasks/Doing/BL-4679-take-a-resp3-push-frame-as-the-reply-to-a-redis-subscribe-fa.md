---
id: BL-4679
title: Take a RESP3 push frame as the reply to a Redis SUBSCRIBE-family command
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extended.Protocol.Redis.UnitLibrary/Client/RedisClientFlow.cs, Curl.Extended.Protocol.Redis.UnitTests/Client/RedisProtocolHandlerTests.cs]
requirement: none
created: 2026-11-03
completed:
---
# BL-4679 — Take a RESP3 push frame as the reply to a Redis SUBSCRIBE-family command

## Goal

`curl redis://host/SUBSCRIBE/chan` (and `PSUBSCRIBE`, `SSUBSCRIBE` and the unsubscribe forms) writes the subscribe confirmation instead of hanging, now that push frames are dropped while waiting for a reply.

## Context

Found by the shift-end review (ADR-0710) of `f37ba71c...dfa29449`: code-reviewer.

BL-4600 made `ReceiveAsync` in `Curl.Extended.Protocol.Redis.UnitLibrary/Client/RedisClientFlow.cs` (~lines 104-113) loop `while (reply.Kind == RespReplyKind.Push)`, dropping every push frame. After `HELLO 3`, Redis answers `SUBSCRIBE`, `PSUBSCRIBE`, `SSUBSCRIBE`, `UNSUBSCRIBE`, `PUNSUBSCRIBE` and `SUNSUBSCRIBE` only with a push frame. The flow now skips the confirmation and every message, and waits until `-m` (exit 28) or until the server closes (exit 56). Before BL-4600 it rendered the confirmation.

Fix: for those commands, take the push frame whose first element names the command as the reply; keep dropping pushes for every other command.

## Acceptance criteria

- [ ] A unit test sends `SUBSCRIBE chan` to a scripted RESP3 server that answers with a push (`subscribe`, `chan`, `1`); the confirmation is rendered and the transfer succeeds.
- [ ] The BL-4600 test that drops an unrelated push before a `GET` reply still passes.
- [ ] The `ReceiveAsync` doc comment and the project's `CLAUDE.md` say which pushes are taken as replies.
- [ ] `dotnet build` is clean and the fast tests of `Curl.Extended.Protocol.Redis.UnitTests` pass.

## Notes

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 15c90db3 a53abaf5 ff509599 711bad4c.
