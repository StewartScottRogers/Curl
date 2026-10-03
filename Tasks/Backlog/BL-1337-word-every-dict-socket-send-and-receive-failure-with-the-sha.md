---
id: BL-1337
title: Word every dict socket send and receive failure with the shared CurlSocketErrorText table
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1325]
touches: [Curl.Protocol.Dict.UnitLibrary, Curl.Protocol.Dict.UnitTests]
requirement: FR-085
created: 2026-10-03
completed:
---
# BL-1337 — Word every dict socket send and receive failure with the shared CurlSocketErrorText table

## Goal

A `dict://` send or receive that fails with any socket error ends with curl 8.21.0's `Send failure: <words>` (exit 55) / `Recv failure: <words>` (exit 56), the words coming from `CurlSocketErrorText` (BL-1325) for the platform, so an aborted connection reads `Recv failure: Connection was aborted` on Windows instead of `Failure when receiving data from the peer`, and a reset reads `Connection reset by peer` off Windows.

## Context

- Today `Curl.Protocol.Dict.UnitLibrary/DictIoFailures.cs` keeps `SendConnectionReset` / `ReceiveConnectionReset` (lines 15 and 21, the Windows words on every platform) and `IsReset` (line 66) recognises only `SocketError.ConnectionReset`; every other socket error falls back to `SendFailedMessage` / `ReceiveFailedMessage` (lines 18 and 24).
- curl 8.21.0 (tag `curl-8_21_0`), `lib/cf-socket.c` lines 1562 and 1618: every socket error but `EAGAIN` is `failf(data, "Send failure: %s" / "Recv failure: %s", curlx_strerror(...))`, with the Winsock table's words on Windows and `strerror`'s elsewhere (BL-1325's Context has the table and the source lines).
- Replace the two reset constants and `IsReset` with `CurlSocketErrorText.SendFailure` / `ReceiveFailure`; keep the fallback texts for an `IOException` that carries no `SocketException`. `IsFallbackText` keeps its meaning (the fallback texts are the ones without a `-v` line of their own; a `Send failure:` / `Recv failure:` text is a `failf` and has one, as today's reset text does).
- Existing tests that pin `Connection was reset` pass on Linux and macOS today only because the Windows words are used everywhere; after this change those literals hold on Windows only. Mark each such test `[OSCondition(OperatingSystems.Windows)]` and add its non-Windows twin (`[OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]`) asserting the exception's own message, as root `CLAUDE.md` asks for platform-specific answers.

## Acceptance criteria

- [ ] A test in `Curl.Protocol.Dict.UnitTests` makes the fake connection's read throw `IOException` wrapping `SocketException(SocketError.ConnectionAborted)` and asserts exit 56 (`CurlExitCode.RecvError`) with message `Recv failure: Connection was aborted`, marked Windows-only, and a non-Windows twin asserting `Recv failure: ` + the `SocketException`'s own message.
- [ ] A test makes the request's send throw the same and asserts exit 55 (`CurlExitCode.SendError`) with `Send failure: Connection was aborted` on Windows, and its non-Windows twin.
- [ ] Every existing reset test is split by platform as Context describes, and an `IOException` with no socket error inside still gives the fallback texts.
- [ ] `DictIoFailures` no longer declares its own `Connection was reset` constants.
- [ ] `dotnet build Curl.Protocol.Dict.UnitTests -warnaserror` is clean; `dotnet test Curl.Protocol.Dict.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Dict.UnitLibrary` reports no failing member.

## Notes

## Log

- 2026-10-03: Created.
