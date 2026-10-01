---
id: BL-961
title: Write standard output in text mode on Windows under -B, as curl's tool does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-633]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-961 — Write standard output in text mode on Windows under -B, as curl's tool does

## Goal

On Windows, `curl -B` writes standard output in C runtime text mode, so every LF written to stdout becomes CRLF; Curl does the same, byte for byte, and nothing else changes.

## Context

- Found while measuring BL-633 (curl 8.21.0, Schannel build, Windows, 2026-09-29) with `Record-CurlExchange.ps1 -Ftp -FtpData 'l1\nl2\r\n'`:
  - `curl -B ftp://h/dir/f.txt` wrote stdout `6c 31 0d 0a 6c 32 0d 0d 0a`: each LF gained a CR, even one already after a CR.
  - `curl -B -I ftp://h/dir/f.txt` wrote its header lines ending `0d 0d 0a`.
  - `curl -B -o out.txt ...` wrote the file unchanged (`6c 31 0a 6c 32 0d 0a`).
  - `curl ftp://h/dir/f.txt;type=a` (no `-B`) wrote stdout unchanged; `curl -B ftp://h/dir/f.txt;type=i` still converted it. The switch is the tool's `-B`, not the transfer type.
- The man page: "This option causes data sent to stdout to be in text mode for Win32 systems." Off Windows nothing changes.
- The stdout writer lives in `Curl.Console` (`CurlCommandRunner` takes `runsOnWindows`).

## Acceptance criteria

- [ ] Re-measured with `Record-CurlExchange.ps1` (`-B` to stdout, `-B -I`, `-B -o file`, `-B` with `-w`, and `-B` with a non-FTP URL such as `file://`), results copied into Notes, before pinning anything.
- [ ] `Curl.Console.UnitTests` pin, with `runsOnWindows: true`, each LF on stdout written as CRLF under `-B`, and with `runsOnWindows: false` stdout unchanged; `-o` output unchanged either way.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Console`.
- [ ] `--ai-help` still states `-B`'s behaviour correctly (no new option).

## Notes

## Log

- 2026-09-29: Created.
- 2026-10-01: Backlog -> Doing.
