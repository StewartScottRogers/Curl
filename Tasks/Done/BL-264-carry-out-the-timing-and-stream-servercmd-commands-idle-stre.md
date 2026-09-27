---
id: BL-264
title: Carry out the timing and stream servercmd commands idle, stream, delay, writedelay, connection-monitor and upgrade in the sws emulation
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-146]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-26
completed: 2026-09-26
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

- [x] Each command is either carried out, with a test pinning sws's behaviour, or kept in
      `UnsupportedServerCommands` with the reason written in the library's `CLAUDE.md`.
- [x] 100% line and branch coverage of `Curl.Conformance.UnitLibrary`, complexity at most 10,
      per `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Read `tests/server/sws.c` at `curl-8_21_0` for each command before pinning it; the findings and
  the design are in ADR-0040 (decided by Claude under Stewart's delegation).
- Carried out: `idle`, `stream`, `writedelay: N`, `connection-monitor`, `upgrade` and `<postcmd>`
  `wait N`. Each connection keeps a timeline of sends (`SwsServerSend`, writes of up to 20
  bytes as sws writes them) timed on a `TimeProvider` the connector now takes (a second
  constructor; the old one uses `TimeProvider.System`).
- Kept unsupported: `delay: N`. sws resets it after every request, so it only acts when a
  connection is accepted while another's request is part-read; faithful emulation needs sws's
  single-threaded interleaving and no vendored case uses it. Reason in the library's `CLAUDE.md`.
- `connection-monitor` follows sws's one server-wide flag: armed by a served request, cleared by
  the first close that records `[DISCONNECT]` (pinned by a two-connection test).
- Simplification (default taken): the monitor is armed when a request is served, not when its
  request line is parsed, so a client closing mid-request does not record `[DISCONNECT]`.
- Added `Documentation/Planning/Decisions` to `touches` for ADR-0040; no task in Doing names it.
- Tests use a hand-written `ManualTimeProvider` in the test project. Conformance library at
  100% line and branch, 0 failing members, per `Measure-CodeQuality.ps1`; 254 conformance tests.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. sws emulation carries out idle, stream, writedelay, connection-monitor, upgrade and postcmd wait on an injected clock; delay stays reported with its reason
