---
id: BL-4600
title: Fix Redis conformance: RESP-Push-1 push frame before the reply is not taken for the reply
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Redis.UnitLibrary/Wire/RespReplyReader.cs, Curl.Extended.Protocol.Redis.UnitLibrary/Client/RedisClientFlow.cs, Curl.Extended.Protocol.Redis.UnitTests, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4600 — Fix Redis conformance: RESP-Push-1 push frame before the reply is not taken for the reply

## Goal

A RESP3 push frame (`>`) that arrives before a command's reply is handled out of band, and the reply that follows is printed.

## Context

Found by BL-4490: `RedisFramingCases.Push_BeforeTheReply_IsNotTakenForTheReply` (`Redis:RESP-Push-1`, `Documentation/Conformance/Extended/Redis.md`). The RESP spec says push data is out of band and never a command's reply. Today `RespReplyReader` reads `>2 +message +hello` as the reply, so curl prints `message\nhello\n` and leaves `+OK` unread. Skip push frames (as attributes are skipped) when reading a command's reply.

## Acceptance criteria

- [x] The case passes: `dotnet test Curl.Conformance.Extended.UnitTests --filter "FullyQualifiedName~Redis"` is green with the line removed.
- [x] A focused unit test in `Curl.Extended.Protocol.Redis.UnitTests` pins the fix (ADR-0482).
- [x] The `KnownFailures.txt` line(s) naming this task are removed.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests are green and the library stays at 100% line and branch coverage (`Measure-CodeQuality.ps1`).

## Notes

- Done directly rather than through the full /feature stages: a one-method fix the task already designs. `RedisClientFlow.ReceiveAsync` now reads frames until one is not a push (`ReceiveFrameAsync` holds the old body); `RespReplyReader` is unchanged and still returns a push as a push, so its own tests stay a plain RESP3 reader. Dropping pushes in the client, not the reader, keeps the reader usable for a later subscribe mode, where pushes are the data.
- Focused test: `RedisProtocolHandlerTests.ExecuteAsync_PushFramesBeforeTheReply_AreDroppedAndTheReplyIsWritten` (two pushes, then `+OK`).
- Measured: Redis unit tests 102 passed, conformance Redis 41 passed with the KnownFailures line removed (it named the old ID BL-4583), build -warnaserror clean, all fast-test projects passed, Measure-CodeQuality: Redis library 100% line and branch, 0 failing members.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: b758e75e 75d02dc0 81c79471 591bd22b.
- 2026-11-02: Doing -> Done. A RESP3 push frame before a command's reply is dropped and the reply is printed (RESP-Push-1 passes)
