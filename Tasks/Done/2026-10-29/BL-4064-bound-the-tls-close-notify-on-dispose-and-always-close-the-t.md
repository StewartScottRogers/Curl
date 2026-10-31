---
id: BL-4064
title: Bound the TLS close_notify on dispose and always close the transport
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary/SslStreamConnection.cs, Curl.Networking.UnitLibrary/HandBuiltTlsConnection.cs, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4064 — Bound the TLS close_notify on dispose and always close the transport

## Goal

Disposing a TLS connection never hangs on sending `close_notify`, and it always closes the underlying stream and socket, whatever the shutdown throws.

## Context

Found by the shift-end review of 2026-10-28 (silent-failure-hunter, ADR-0710), in the diff `354cb7711...c4b167258` (BL-3984, ADR-0959).

- `Curl.Networking.UnitLibrary/SslStreamConnection.cs:153-176`: `DisposeAsync` awaits `SendCloseNotifyAsync`, which calls `sslStream.ShutdownAsync()`. That call takes no token and has no time limit.
- `HandBuiltTlsConnection.cs:123-146` calls `ShutdownAsync(tlsStream, CancellationToken.None)`.
- Either can block on a full socket send buffer when the peer stops reading. Example: `curl -m 5 -T big https://host/` where the server stops reading. `-m` fires, the connection is disposed, and the close never returns, so curl never exits with 28. curl's `SSL_shutdown` runs on a non-blocking socket and gives up.
- Only `IOException` and `InvalidOperationException` are caught. Any other exception (for example `NotSupportedException` from a concurrent write on `SslStream`, or `OperationCanceledException` once a token is added) skips `sslStream.DisposeAsync()` and `plaintext.DisposeAsync()`, which leaks the socket.
- The SSH handler bounds its equivalent close with `ShutDownTimeout` (CGR-270).

## Acceptance criteria

- [x] Both connections send `close_notify` under a short bounded token, about as `SshProtocolHandler.ShutDownTimeout` does. A timeout or cancellation of that send is ignored.
- [x] Both connections dispose the TLS stream and the transport in a `finally`, so they are disposed whatever the close throws.
- [x] A test in `Curl.Networking.UnitTests` uses a transport whose writes never complete. It checks that `DisposeAsync` returns within the bound and that the transport is disposed.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Bound is CloseNotifyTimeout (5 s, as SshProtocolHandler.ShutDownTimeout) on a TimeProvider seam. The close_notify send ignores every exception (best effort, as curl); a failing TLS stream disposal is swallowed so the transport is always closed. The stall test uses SslStream; the hand-built connection shares the code shape and is tested through the disposal path.
- CI on Linux and macOS: the tests use in-memory streams only, so they are platform-neutral.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 9fba8f25 3a28e2c3 2d682041 3d836d5e.
- 2026-10-29: Doing -> Done. close_notify bounded, transport always closed, tests added
