---
id: BL-264
title: Carry out the timing and stream servercmd commands idle, stream, delay, writedelay, connection-monitor and upgrade in the sws emulation
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-146]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-264 — Carry out the timing and stream servercmd commands idle, stream, delay, writedelay, connection-monitor and upgrade in the sws emulation

## Goal

`SwsHttpServerConnector` carries out, or deliberately keeps reporting with a recorded reason,
each of `idle`, `stream`, `delay: N`, `writedelay: N`, `connection-monitor`, `upgrade` and the
`<postcmd>` `wait N` command, so no case is skipped for one of them without a decision.

## Context

- BL-146 built the emulation and reports these commands as unsupported (`SwsServerCommands`);
  it does not look at `<postcmd>` at all.
- Upstream: `tests/server/sws.c` at `curl-8_21_0`: `idle` sends nothing and keeps the
  connection; `stream` sends `a string to stream 01234567890\n` forever; `delay` and
  `writedelay` sleep after connect and between 20-byte writes; `connection-monitor` writes
  `[DISCONNECT]` to the protocol dump when a connection closes; `upgrade` switches protocol
  on an `Upgrade:` request; `<postcmd>` `wait N` sleeps after the reply.
- Timing must come from an injected `TimeProvider` (CLAUDE.md); no `Thread.Sleep`. A read that
  would wait forever must honour its cancellation token.

## Acceptance criteria

- [ ] Each command is either carried out, with a test pinning sws's behaviour, or kept in
      `UnsupportedServerCommands` with the reason written in the library's `CLAUDE.md`.
- [ ] 100% line and branch coverage of `Curl.Conformance.UnitLibrary`, complexity at most 10,
      per `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-09-26: Created.
