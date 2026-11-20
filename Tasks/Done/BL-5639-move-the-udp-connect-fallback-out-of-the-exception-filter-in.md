---
id: BL-5639
title: Move the UDP connect fallback out of the exception filter in PortUnreachableReportingDatagramChannel
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary/PortUnreachableReportingDatagramChannel.cs, Curl.Networking.UnitTests/PortUnreachableFallbackToNextAddressTests.cs]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-5639 — Move the UDP connect fallback out of the exception filter in PortUnreachableReportingDatagramChannel

## Goal

A failure other than `SocketException` while falling back to the next address on a UDP connect propagates and leaves no channel undisposed, as the send-path fallback (`OpenAndResendAsync`) already does.

## Context

Found by the shift-end review of 2026-11-20 (ADR-0710; silent-failure pass over `7c1689d7d4...e89e78f09c`, commit `e89e78f09c`).

`Curl.Networking.UnitLibrary/PortUnreachableReportingDatagramChannel.cs:44-51` runs `ConnectToNextAddress()` inside the exception filter `catch (SocketException) when (ConnectToNextAddress())`. In .NET an exception thrown inside a filter is swallowed and the filter counts as `false`. So if `openChannel(...)` or the new channel's `ConnectToServer()` throws anything but `SocketException` (an `ObjectDisposedException`, `ArgumentException`, `InvalidOperationException`), that failure vanishes, the original connect `SocketException` surfaces instead (CoAP reports it as exit 55 `CoAP connect failed`), and the channel opened in `ConnectToNextAddress` (line 507 onward) is neither made current nor added to `_retired`, so it is never disposed and its UDP socket leaks. The filter also mutates `_inner`, `_refused` and `_nextAlternative` before the inner call's `finally` blocks have run. The send path's doc says "any other failure propagates" and disposes the opened channel; the connect path should behave the same.

## Acceptance criteria

- [x] `ConnectToServer` catches the `SocketException` in an ordinary `catch` block and calls the fallback there, rethrowing the original (`throw;`) when no address is left; no side-effecting exception filter remains.
- [x] A test where the next address's `ConnectToServer` throws `InvalidOperationException` sees that exception escape `ConnectToServer` and that opened channel disposed once the wrapper is disposed (or at once).
- [x] The existing `ConnectToServer_FirstAddressFails_ConnectsTheNextAndSwitchesToIt` and `ConnectToServer_EveryAddressFails_ThrowsTheFirstFailureUntranslated` tests still pass.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

ConnectToNextAddress now retires (disposes with the wrapper) a channel opened then failing with a non-SocketException and rethrows it; ConnectToServer uses an ordinary catch. Quality run: 0 failing members. CI on Linux/macOS: tests platform-neutral.

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing. Criteria at claim: d3f52104 050932e0 8daaca4d 3d836d5e.
- 2026-11-20: Doing -> Done. UDP connect fallback moved out of the exception filter; tests added
