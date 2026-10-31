---
id: BL-4356
title: WireGuard extension: rekey a tunnel session and send keepalives during a long transfer
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4147, BL-4354, BL-4371, BL-4372]
touches: [Curl.Extension.WireGuard.UnitLibrary/Tunnel, Curl.Extension.WireGuard.UnitTests/Tunnel, Curl.Extension.WireGuard.UnitTests/IpStack/ManualTimeProvider.cs, Curl.Extension.WireGuard.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-10-30
completed: 2026-10-30
---
# BL-4356 — WireGuard extension: rekey a tunnel session and send keepalives during a long transfer

## Goal

A transfer through `--wireguard` that lasts longer than Rekey-After-Time (120 s) keeps running: the tunnel rekeys its session and sends keepalives as the whitepaper says.

## Context

BL-4147 (ADR-1159) gives each connection one transport session; `WireGuardTransportSession.Seal` refuses past Reject-After-Time (180 s), so a longer transfer fails. Whitepaper sections 6.2 (rekey by the initiator after Rekey-After-Time or Rekey-After-Messages, keeping the old session to receive until the new one confirms) and 6.5 (passive keepalive after Keepalive-Timeout, and `PersistentKeepalive`) say what to add to `WireGuardPacketTunnel`. Test with `TunnelTestPeer` and a `ManualTimeProvider`.

## Acceptance criteria

- [x] After Rekey-After-Time the tunnel sends a new initiation, moves sending to the new session once its first message arrives, and still opens packets of the old session until then; unit tests named after whitepaper section 6.2.
- [x] A keepalive is sent after Keepalive-Timeout with nothing to send, and every `PersistentKeepalive` seconds when configured; unit tests named after section 6.5.
- [x] ADR-1159's Left out section is updated; the four quality gates hold by Measure-CodeQuality.ps1.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- `WireGuardPacketTunnel` now owns the initiator and the sessions: a send once the session `NeedsRekey` also sends a new initiation (again after each Rekey-Timeout without a response); the response moves sending to the new session and keeps the old one to open late packets until the next rekey. Passive keepalive after Keepalive-Timeout and `PersistentKeepalive` timers on the injected clock. `WireGuardConnection` and `WireGuardTunnelLifetime` dispose the tunnel instead of one session; both connectors open it through `WireGuardPacketTunnel.OpenAsync`. Decisions and what is left out (receive-side trigger, Rekey-Attempt-Time give-up, responder role) recorded in ADR-1159.
- Widened touches to `Curl.Extension.WireGuard.UnitTests/IpStack/ManualTimeProvider.cs` (its wall clock was real time, so two initiations within the 16.8 ms TAI64N granularity were refused by the test responder; it now starts at a fixed instant, moves with `Advance` and steps 20 ms per reading) and the library's `CLAUDE.md` (it said "No rekeying"). No task in Doing on origin/work/dark-factory touches either.
- Measure-CodeQuality.ps1 -Library Curl.Extension.WireGuard.UnitLibrary: 0 failing members. Full build clean, fast tests green on Windows; the tests use only the manual clock and in-memory peer, so they are platform-neutral.

## Log

- 2026-10-30: Created.
- 2026-10-30: Backlog -> Doing. Criteria at claim: b22b485f 0cba0a1a 8d6b4c2f 3d836d5e.
- 2026-10-30: Doing -> Done. A --wireguard tunnel rekeys after Rekey-After-Time and sends passive and PersistentKeepalive keepalives
