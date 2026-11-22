---
id: BL-5748
title: Send TELNET standard input bytes unchanged on Windows; CR LF stays CR LF
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Telnet.UnitLibrary/TelnetProtocolHandler.cs]
requirement: none
created: 2026-11-22
completed:
---
# BL-5748 — Send TELNET standard input bytes unchanged on Windows; CR LF stays CR LF

## Goal

`curl telnet://host:port` reading standard input (without `-T -`) sends the input bytes unchanged on Windows, as upstream curl does: CR LF is sent as CR LF.

## Context

From Surly cookbook bug report `2026-11-22T0726-Curl-bug-report.md` (batch 10 of the 1,000-recipe plan), finding 4. Build tested: Curl `c84035f797`, Windows 11; server Surl `5da5b3eaf`. Check it still reproduces on the current branch first.

`printf 'PING\r\n' | curl telnet://127.0.0.1:4222` against surl's NATS listener: surl answers `-ERR 'Parser Error'` because it received `PING\n`. Upstream curl reads stdin in binary and sends the bytes unchanged. The port likely reads standard input through a text reader on this path (`Console.In`, a `StreamReader` line read, or `ReadLine` plus `\n`), which drops the CR. Read stdin as a byte stream (`Console.OpenStandardInput()`) on the interactive TELNET path, as the `-T -` path presumably does, and check Linux and macOS behave the same.

## Acceptance criteria

- [x] Withdrawn, premise false: Windows curl 8.22.0 sends CR LF as LF (ADR-0848); Curl matches it, pinned by existing tests.
- [x] Withdrawn, same reason: non-text bytes follow ADR-0848's text-mode read, already pinned.
- [x] `dotnet build` is clean and the fast tests pass: no code changed.

## Notes

- Not a defect; no code changed. The CR LF to LF read is deliberate: ADR-0848 (CGR-112) measured the local curl 8.22.0
  (`x86_64-w64-mingw32`, Schannel) reading a `telnet://` transfer's standard input in the C runtime's text mode on
  Windows, so `a` CR LF `b` CR LF reaches the wire as `a` LF `b` LF; only `-T -` sends every byte. The report's premise
  that upstream sends the bytes unchanged holds on Linux and macOS (where Curl already does) but not on the Windows
  build Curl matches. The logic lives in `Curl.Engine.UnitLibrary/TransferContextFactory.cs`, not the telnet handler.
- Decision (Claude under Stewart's delegation): keep ADR-0848's behaviour, as the drop-in rule is to match the platform's
  curl. A caller needing CR LF on the wire on Windows uses `-T -`, as with curl. A re-measure here hung and was abandoned;
  ADR-0848's measurement stands.
- The acceptance criteria were written on the wrong premise and are not met as stated; closed as "not a bug".

## Log

- 2026-11-22: Created from Surly bug report 2026-11-22T0726, finding 4.
- 2026-11-22: Backlog -> Doing. Criteria at claim: 193c7f95 3e043be4 3d836d5e.
