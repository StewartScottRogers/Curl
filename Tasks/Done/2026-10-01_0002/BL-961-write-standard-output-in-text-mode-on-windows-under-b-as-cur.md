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
completed: 2026-10-01
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

- [x] Re-measured with `Record-CurlExchange.ps1` (`-B` to stdout, `-B -I`, `-B -o file`, `-B` with `-w`, and `-B` with a non-FTP URL such as `file://`), results copied into Notes, before pinning anything.
- [x] `Curl.Console.UnitTests` pin, with `runsOnWindows: true`, each LF on stdout written as CRLF under `-B`, and with `runsOnWindows: false` stdout unchanged; `-o` output unchanged either way.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Console`.
- [x] `--ai-help` still states `-B`'s behaviour correctly (no new option).

## Notes

Measured 2026-10-01, curl 8.21.0 (x86_64-w64-mingw32, Schannel), `Record-CurlExchange.ps1`
(`-Ftp -FtpData 'l1\nl2\r\n'`, and `file://` of the bytes `61 0a 62 0d 0a`), all `-s`, exit 0:

| Command | stdout |
| --- | --- |
| `-B ftp://h/dir/f.txt` | `6c 31 0d 0a 6c 32 0d 0d 0a` |
| `ftp://h/dir/f.txt` (no `-B`) | `6c 31 0a 6c 32 0d 0a` |
| `-B -I ftp://...` | header lines each ending `0d 0d 0a` |
| `-B -o out.txt ftp://...` | empty; the file `6c 31 0a 6c 32 0d 0a`, unchanged |
| `-B -w 'w\n' ftp://...` | `6c 31 0d 0a 6c 32 0d 0d 0a 77 0d 0a` |
| `-B -o out.txt -w 'w\n' ftp://...` | `77 0d 0a` |
| `-B file:///.../in.txt` | `61 0d 0a 62 0d 0d 0a` |
| `file:///.../in.txt` (no `-B`) | `61 0a 62 0d 0a` |
| `-B -i http://...` (empty 200) | `HTTP/1.1 200 OK\r\r\nContent-Length: 0\r\r\n\r\r\n` |
| `F --next -B F` (file URL) | `61 0a 62 0d 0a 61 0a 62 0d 0a`: binary mode is sticky |
| `-B F --next F` | `61 0d 0a 62 0d 0d 0a 61 0a 62 0d 0a` |
| `-B -w 'w\n' F F` | `61 0d 0a 62 0d 0d 0a 77 0d 0a` twice |
| `-B -D - -o out.txt ftp://...` | FTP reply lines ending `0d 0a`, not converted (left as is; not in scope) |

Model (curl's tool calls `set_binmode(stdout)` for a transfer to stdout only without `-B`): a
`-B` transfer never switches standard output to binary mode, and on Windows its body to
standard output goes through `TextModeUntilBinaryStream`, so it stays raw once an earlier
transfer switched to binary. `-w`, `--trace -` and `-c -` follow automatically, since they
already key off the same switch. Changes in `CurlCommandRunner`: `SwitchesStandardOutputToBinary`,
`UrlFromSwitchesStandardOutputToBinary` and `TransferAsync` honour `UseAscii`; new
`InTextModeUnderUseAscii` wraps the stdout body stream. `TextModeUntilBinaryStream` gained
`WriteAsync` / `FlushAsync` overrides so async body writes stay async.

Tests: `CurlCommandRunnerUseAsciiTests` (9) and two in `TextModeUntilBinaryStreamTests`.
`Measure-CodeQuality.ps1 -Library Curl.Console`: 100 / 100, 0 failing members. `--ai-help all`
already quotes the man page's "data sent to stdout to be in text mode for Win32 systems",
which is now true; no change needed.

## Log

- 2026-09-29: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. On Windows, -B writes each LF of a body sent to stdout as CRLF, as curl's Schannel build does; -o files and non-Windows unchanged
