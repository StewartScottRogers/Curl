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

- [ ] Piping `PING\r\n` into `curl telnet://...` sends exactly `PING\r\n` on the wire, pinned by a test with a loopback server and a byte stream as standard input.
- [ ] Bytes that are not text (a NUL, a lone CR, 0xFF other than IAC handling) pass through as curl 8.22.0 sends them, pinned by a test.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-22: Created from Surly bug report 2026-11-22T0726, finding 4.
