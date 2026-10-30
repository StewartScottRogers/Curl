---
id: BL-4351
title: WireGuard extension: rekey a tunnel session and send keepalives during a long transfer
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4147, BL-4349, BL-4350]
touches: [Curl.Extension.WireGuard.UnitLibrary/Tunnel, Curl.Extension.WireGuard.UnitTests/Tunnel]
requirement: none
created: 2026-10-30
completed:
---
# BL-4351 — WireGuard extension: rekey a tunnel session and send keepalives during a long transfer

## Goal

A transfer through `--wireguard` that lasts longer than Rekey-After-Time (120 s) keeps running: the tunnel rekeys its session and sends keepalives as the whitepaper says.

## Context

BL-4147 (ADR-1150) gives each connection one transport session; `WireGuardTransportSession.Seal` refuses past Reject-After-Time (180 s), so a longer transfer fails. Whitepaper sections 6.2 (rekey by the initiator after Rekey-After-Time or Rekey-After-Messages, keeping the old session to receive until the new one confirms) and 6.5 (passive keepalive after Keepalive-Timeout, and `PersistentKeepalive`) say what to add to `WireGuardPacketTunnel`. Test with `TunnelTestPeer` and a `ManualTimeProvider`.

## Acceptance criteria

- [ ] After Rekey-After-Time the tunnel sends a new initiation, moves sending to the new session once its first message arrives, and still opens packets of the old session until then; unit tests named after whitepaper section 6.2.
- [ ] A keepalive is sent after Keepalive-Timeout with nothing to send, and every `PersistentKeepalive` seconds when configured; unit tests named after section 6.5.
- [ ] ADR-1150's Left out section is updated; the four quality gates hold by Measure-CodeQuality.ps1.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-30: Created.
