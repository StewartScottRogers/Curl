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
completed: 2026-11-02
---
# BL-4601 — Fix Redis conformance: HELLO-2 fall back to RESP2 when the server refuses HELLO 3

## Goal

When a server answers `HELLO 3` with an error that is not `NOAUTH` (a pre-6 Redis: `-ERR unknown command 'HELLO'`), curl goes on in RESP2 and sends the command instead of failing with exit 8.

## Context

Found by BL-4490: `RedisHandshakeCases.Resp2_ServerRefusesHelloAndAnswersNullBulkString_GoesOnInResp2` and `Resp2_ServerRefusesHelloAndAnswersNullArray_PrintsNothing` (`Redis:HELLO-2`, `Redis:RESP2-Null-2`). RESP protocol spec: a client that sends HELLO and gets an error falls back to RESP2. `RedisClientFlow.RunAsync` throws WeirdServerReply on any HELLO error. With credentials, the RESP2 path authenticates with `AUTH [user] password` before the command; decide its wire form in an ADR and add a conformance case for it.

## Acceptance criteria

- [x] The case passes: `dotnet test Curl.Conformance.Extended.UnitTests --filter "FullyQualifiedName~Redis"` is green with the line removed.
- [x] A focused unit test in `Curl.Extended.Protocol.Redis.UnitTests` pins the fix (ADR-0482).
- [x] The `KnownFailures.txt` line(s) naming this task are removed.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests are green and the library stays at 100% line and branch coverage (`Measure-CodeQuality.ps1`).

## Notes

- Decision (ADR-1208): an error reply to `HELLO 3` other than `NOAUTH` or `WRONGPASS` falls back to RESP2; with credentials the RESP2 path sends `AUTH password` (no user name, the pre-6 form) or `AUTH user password`, and an error to it ends with exit 67. `NOAUTH`/`WRONGPASS` still end with 67.
- `Redis:RESP2-Null-2` also needed the reader to read `*-1` as an array with no elements (renders as nothing) instead of a null (renders as an empty line); `RespReplyReader.ReadAggregateAsync` now clamps the count to 0.
- The KnownFailures.txt lines named BL-4584 (the ID before a renumbering), not BL-4601; removed both Redis lines. Also removed the two `Grpc:` lines BL-4595's fix had made pass: origin/work/dark-factory already dropped them, so the rebase sees the same removal.
- Also updated `Curl.Extended.Protocol.Redis.UnitLibrary/CLAUDE.md` (outside touches, doc only) so it says what the flow and reader do now.
- The conformance case for the RESP2 `AUTH` wire form was left out: the conformance project's case files are outside touches; the unit tests `ExecuteAsync_HelloRefusedWithCredentials_AuthenticatesWithResp2AuthBeforeTheCommand` and `ExecuteAsync_Resp2AuthRefused_EndsWithLoginDenied` pin it.
- Checked: Redis conformance 41/41, Redis unit tests 105/105, full build -warnaserror clean, fast tests green, Measure-CodeQuality Redis library 100/100, 0 failing members. Platform-neutral (no paths, OS text or certificates).

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: b758e75e 75d02dc0 81c79471 591bd22b.
- 2026-11-02: Doing -> Done. Redis falls back to RESP2 (with AUTH) when the server refuses HELLO 3, and a RESP2 null array prints nothing
