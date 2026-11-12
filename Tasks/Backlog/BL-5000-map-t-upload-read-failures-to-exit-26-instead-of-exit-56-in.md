---
id: BL-5000
title: Map nats -T upload read failures to exit 26 instead of exit 56
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extended.Protocol.Nats.UnitLibrary/NatsProtocolHandler.cs]
requirement: none
created: 2026-11-11
completed:
---
# BL-5000 — Map nats -T upload read failures to exit 26 instead of exit 56

## Goal

A `-T` upload that fails while `nats://` reads it ends with curl's read-error exit 26 and a local-file message, not exit 56 "Recv failure: Connection was reset".

## Context

Found by the shift-end review of 2026-11-11 (ADR-0710), silent-failure-hunter, in the diff e8341591d..4287a89b7.

`Curl.Extended.Protocol.Nats.UnitLibrary/NatsProtocolHandler.cs:37` adds `catch (IOException)` around `TransferAsync`, returning `CurlExitCode.RecvError` with the fixed text "Recv failure: Connection was reset". The same change makes `TransferAsync` (line 49) read the whole `-T` upload through `RequestBodyAsync` (line 79, `upload.CopyToAsync`) inside that `try`. An `IOException` from the local upload stream (a file that becomes unreadable or a pipe that fails mid-copy) is therefore reported as a network reset with exit 56. curl reports a failed upload read as exit 26 (`CURLE_READ_ERROR`). The fixed message also hides the exception's own text for every network-side fault.

The SignalR and Socket.IO handlers got the same catch but do not read the upload, and the STOMP and XMPP `RequestBodyAsync` calls sit under typed catches only, so they are not affected.

## Acceptance criteria

- [ ] `RequestBodyAsync` in `NatsProtocolHandler.cs` runs outside the `catch (IOException)` that maps to `RecvError`, or its `IOException` is caught on its own and mapped to `CurlExitCode.ReadError`.
- [ ] A unit test in `Curl.Extended.Protocol.Nats.UnitTests` gives a `-T` upload stream that throws `IOException` on read and asserts exit 26, not 56.
- [ ] The existing NATS test for a connection reset still asserts exit 56.
- [ ] `dotnet build` is clean and the fast tests pass; the library stays at 100% line and branch coverage.

## Notes

## Log

- 2026-11-11: Created.
