---
id: BL-4422
title: Re-check the SSH channel's wait condition inside the read gate so concurrent read and write cannot hang
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Ssh.UnitLibrary/Connection/SshChannel.cs, Curl.Ssh.UnitTests/Connection]
requirement: none
created: 2026-10-31
completed:
---
# BL-4422 — Re-check the SSH channel's wait condition inside the read gate so concurrent read and write cannot hang

## Goal

A direct-tcpip SSH channel pumped in both directions at once never blocks on the wire for a packet its caller no longer needs, and its window and receive queue stay consistent under concurrent `ReadAsync` and `SendAsync`.

## Context

Found by the shift-end review of 2026-10-31 (ADR-0710; code-reviewer rated it Critical, silent-failure-hunter and type-design-analyzer found it too) in the merge `55fa1ce15...ac0e3e86e`.

- `Curl.Ssh.UnitLibrary/Connection/SshChannel.cs:243-262`: the new `readGate` serialises packet reads in `ReadNextMessageAsync`, but its callers test their condition before taking the gate and never again after. `SendAsync` (`:128-136`) loops on `remoteWindow == 0`; when a concurrent `ReadAsync` holds the gate and receives the server's `CHANNEL_WINDOW_ADJUST`, the writer then takes the gate and still blocks on `transport.PacketReader.ReadAsync`. If the server is waiting for our data, nothing more arrives and the transfer hangs. The mirror case: the writer's read queues the only `CHANNEL_DATA` into `received` while `ReadAsync` (`:93-106`) waits, and the reader then blocks on the wire with data already queued.
- `remoteWindow` is changed without a lock on both sides (`SendAsync` subtracts at `:144`, the window-adjust handler adds at `:289`), and `received` is a plain `Queue` that `ReadAsync` dequeues while another call's `ReadNextMessageAsync` enqueues. An adjustment can be lost (send past the window, an RFC 4254 violation, or a stalled upload).
- `:130-133`: `SendAsync` now throws `EndOfStreamException` when `remoteSentEof` is set and the window is used up. RFC 4254 §5.3 says EOF closes only the server's direction; the server can still send `WINDOW_ADJUST` and take data. Only `remoteClosed` should end a send. The test `WriteAsync_Rfc4254Section5_3_PeerSentEofWhileWindowIsUsedUp_ThrowsEndOfStream` pins the wrong behaviour and must change.

## Acceptance criteria

- [ ] Inside `readGate`, `ReadNextMessageAsync` (or its callers) re-checks the caller's wait condition and returns without reading when it already holds: `remoteWindow > 0 || remoteClosed` for a send, queued data or EOF/close for a read.
- [ ] `remoteWindow`, `received` and `unread` are only read and changed under one lock (or `Interlocked`), so no window adjustment or queued packet is lost.
- [ ] A unit test in `Curl.Ssh.UnitTests/Connection` drives a concurrent read and send where the reader receives the window adjustment the writer waits for, and the send completes without a further packet from the server.
- [ ] A unit test where the sender's read queues the only `CHANNEL_DATA` shows the waiting `ReadAsync` returns it without another packet.
- [ ] `SendAsync` throws only on `remoteClosed`; a test shows a send after the server's EOF waits for and uses a later `WINDOW_ADJUST`, and the old `..._PeerSentEofWhileWindowIsUsedUp_ThrowsEndOfStream` test is replaced.
- [ ] `dotnet build` is clean and the fast tests pass; `Curl.Ssh.UnitLibrary` stays at 100% line and branch coverage.

## Notes

## Log

- 2026-10-31: Created.
- 2026-10-31: Backlog -> Doing. Criteria at claim: 11f64556 52bcb553 a979ebb2 0f6dcf32 5a8c7061 caa30fdb.
