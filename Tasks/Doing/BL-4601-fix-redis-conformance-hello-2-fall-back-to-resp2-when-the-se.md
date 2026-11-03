---
id: BL-4601
title: Fix Redis conformance: HELLO-2 fall back to RESP2 when the server refuses HELLO 3
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Redis.UnitLibrary/Wire/RespReplyReader.cs, Curl.Extended.Protocol.Redis.UnitLibrary/Client/RedisClientFlow.cs, Curl.Extended.Protocol.Redis.UnitTests, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4601 — Fix Redis conformance: HELLO-2 fall back to RESP2 when the server refuses HELLO 3

## Goal

When a server answers `HELLO 3` with an error that is not `NOAUTH` (a pre-6 Redis: `-ERR unknown command 'HELLO'`), curl goes on in RESP2 and sends the command instead of failing with exit 8.

## Context

Found by BL-4490: `RedisHandshakeCases.Resp2_ServerRefusesHelloAndAnswersNullBulkString_GoesOnInResp2` and `Resp2_ServerRefusesHelloAndAnswersNullArray_PrintsNothing` (`Redis:HELLO-2`, `Redis:RESP2-Null-2`). RESP protocol spec: a client that sends HELLO and gets an error falls back to RESP2. `RedisClientFlow.RunAsync` throws WeirdServerReply on any HELLO error. With credentials, the RESP2 path authenticates with `AUTH [user] password` before the command; decide its wire form in an ADR and add a conformance case for it.

## Acceptance criteria

- [ ] The case passes: `dotnet test Curl.Conformance.Extended.UnitTests --filter "FullyQualifiedName~Redis"` is green with the line removed.
- [ ] A focused unit test in `Curl.Extended.Protocol.Redis.UnitTests` pins the fix (ADR-0482).
- [ ] The `KnownFailures.txt` line(s) naming this task are removed.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests are green and the library stays at 100% line and branch coverage (`Measure-CodeQuality.ps1`).

## Notes

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: b758e75e 75d02dc0 81c79471 591bd22b.
