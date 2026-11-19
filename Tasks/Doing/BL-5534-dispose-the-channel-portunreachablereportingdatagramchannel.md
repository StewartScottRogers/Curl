---
id: BL-5534
title: Dispose the channel PortUnreachableReportingDatagramChannel opened when its resend fails with a non-socket exception
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary/PortUnreachableReportingDatagramChannel.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5534 — Dispose the channel PortUnreachableReportingDatagramChannel opened when its resend fails with a non-socket exception

## Goal

PortUnreachableReportingDatagramChannel disposes the channel it opened to the next address whenever the resend fails, not only on a SocketException.

## Context

Found by the shift-end review of 2026-11-18 (silent-failure-hunter, ADR-0710), diff 2a473a41ba..43a2df5f77.
In `SwitchToNextAddressAsync` (`Curl.Networking.UnitLibrary/PortUnreachableReportingDatagramChannel.cs:96-114`), `opened` is disposed only inside `catch (SocketException)`. While the saved datagrams are resent, other exceptions can escape with `opened` not yet assigned to `_inner`: an OperationCanceledException when `-m` or `--connect-timeout` fires, an ObjectDisposedException, or an IOException from a tunnelled channel. `DisposeAsync` then closes only the old refused channel, and the new socket stays open until finalization.

## Acceptance criteria

- [ ] Any exception during the open-and-resend step disposes `opened` before it propagates. A SocketException still moves on to the next address, as it does now.
- [ ] A unit test cancels during the resend and shows the newly opened stand-in channel was disposed and the OperationCanceledException propagates.
- [ ] `dotnet build` is clean, the fast tests pass, and Curl.Networking.UnitLibrary keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-18: Created.
- 2026-11-18: Backlog -> Doing. Criteria at claim: bb176189 843896d4 68cb9cf5.
