---
id: BL-1344
title: Word every smb socket send and receive failure with the shared CurlSocketErrorText table
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1325]
touches: [Curl.Protocol.Smb.UnitLibrary, Curl.Protocol.Smb.UnitTests]
requirement: FR-085
created: 2026-10-03
completed:
---
# BL-1344 — Word every smb socket send and receive failure with the shared CurlSocketErrorText table

## Goal

An `smb://` message send or read that fails with any socket error ends with curl 8.21.0's `Send failure: <words>` (exit 55) / `Recv failure: <words>` (exit 56), the words from `CurlSocketErrorText` (BL-1325) for the platform, instead of the fallback texts for every error but a reset.

## Context

- Today `Curl.Protocol.Smb.UnitLibrary/SmbIoFailures.cs` (`SendFailed`, `ReceiveFailed`, `IsReset` at line 30) picks `SmbMessages.SendConnectionReset` / `ReceiveConnectionReset` (`SmbMessages.cs` lines 28 and 34, the Windows words on every platform) only for `SocketError.ConnectionReset`, else `SendFailed` / `ReceiveFailed` (lines 31 and 37). BL-1231 added these.
- curl 8.21.0 (tag `curl-8_21_0`), `lib/cf-socket.c` lines 1562 and 1618: every socket error but `EAGAIN` is `failf(data, "Send failure: %s" / "Recv failure: %s", curlx_strerror(...))`, Winsock words on Windows and `strerror`'s elsewhere (BL-1325's Context has the table). `lib/smb.c` sends and receives through the same socket filter.
- Replace the reset constants and `IsReset` with `CurlSocketErrorText.SendFailure` / `ReceiveFailure`, keeping the fallback texts for an `IOException` with no `SocketException`. `SmbMessages` is public; remove only the two reset constants, and check nothing outside the library uses them (`grep -r SendConnectionReset`).
- Existing tests pinning `Connection was reset` hold on Windows only after this change: mark each `[OSCondition(OperatingSystems.Windows)]` and add a non-Windows twin (`[OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]`) asserting the exception's own message.

## Acceptance criteria

- [ ] A test in `Curl.Protocol.Smb.UnitTests` makes a read throw `IOException` wrapping `SocketException(SocketError.ConnectionAborted)` and asserts exit 56 with `Recv failure: Connection was aborted` (Windows-only), plus a non-Windows twin with `Recv failure: ` + the exception's own message.
- [ ] A test makes a send fail the same way and asserts exit 55 with `Send failure: Connection was aborted` (Windows) and its twin.
- [ ] Every existing reset test is split by platform; an `IOException` with no socket error still gives the fallback texts.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean (the public constants' removal breaks no other project); `dotnet test Curl.Protocol.Smb.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Smb.UnitLibrary` reports no failing member.

## Notes

## Log

- 2026-10-03: Created.
