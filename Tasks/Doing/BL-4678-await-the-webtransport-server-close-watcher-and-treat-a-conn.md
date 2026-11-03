---
id: BL-4678
title: Await the WebTransport server-close watcher and treat a CONNECT stream FIN or reset as a server close
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extended.Protocol.WebTransport.UnitLibrary/Session/WebTransportSession.cs, Curl.Extended.Protocol.WebTransport.UnitTests/WebTransportSessionTests.cs]
requirement: none
created: 2026-11-03
completed:
---
# BL-4678 — Await the WebTransport server-close watcher and treat a CONNECT stream FIN or reset as a server close

## Goal

The WebTransport session's server-close watcher is stopped and awaited before its cancellation source is disposed, and a CONNECT stream that the server ends (FIN or reset) without `WT_CLOSE_SESSION` ends the session promptly instead of waiting for `-m`.

## Context

Found by the shift-end review (ADR-0710) of `f37ba71c...dfa29449`: silent-failure-hunter; type-design-analyzer and code-reviewer noted the same watcher.

`Curl.Extended.Protocol.WebTransport.UnitLibrary/Session/WebTransportSession.cs:46` starts `_ = WatchForServerCloseAsync(connect, exchangeCancellation);` and never awaits it (body ~lines 83-102):
- A read failure in the watcher is an unobserved exception, and the exchange is never cancelled.
- A FIN on the CONNECT stream just ends the loop. WebTransport over HTTP/3 treats that as closing the session with code 0, so a unidirectional or datagram transfer waits for `-m` (exit 28).
- `exchangeCancellation` is disposed by `using` while the watcher still reads, so a late close capsule makes `CancelAsync()` throw `ObjectDisposedException` in the unobserved task.
- The `catch (Exception) when (serverClose is { })` at ~line 52 relabels unrelated errors, such as an output write failure (exit 23), as exit 56.

ADR-1216 pins exit 56 on a server close; keep that.

## Acceptance criteria

- [ ] The watcher task is cancelled and awaited before `exchangeCancellation` is disposed, on both the success and the failure path.
- [ ] A FIN or reset on the CONNECT stream with no close capsule cancels the exchange and ends the transfer as ADR-1216's server close does (amend the ADR if the exit code differs).
- [ ] An output write failure during the exchange still ends with exit 23, even when the server also closes.
- [ ] `WebTransportSessionTests` covers each of the three cases above.
- [ ] `dotnet build` is clean and the fast tests of `Curl.Extended.Protocol.WebTransport.UnitTests` pass.

## Notes

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: f310805a 37cb756b c09bc496 370ef6b5 e5bbbb16.
