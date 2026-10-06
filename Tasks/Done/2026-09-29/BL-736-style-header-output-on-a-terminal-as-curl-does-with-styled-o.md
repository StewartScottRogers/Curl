---
id: BL-736
title: Style header output on a terminal as curl does with --styled-output
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-489]
touches: [Curl.Output.UnitLibrary, Curl.Output.UnitTests, Curl.Console, Curl.Console.UnitTests, Documentation/Planning/Decisions/ADR-0246-styled-output-bolds-header-names-and-links-location-on-a-terminal-as-curl-does.md]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-736 — Style header output on a terminal as curl does with --styled-output

## Goal

When standard output is a terminal and `--styled-output` is on (the default), header output from `-i`, `-I` and `-D -` is styled exactly as curl 8.21.0 styles it (bold header names, and the `Location:` value as an OSC 8 hyperlink where curl writes one), `--no-styled-output` turns it off, and redirected output is never styled, on every platform.

## Context

- Conformance audit 2026-09-28, row 2; standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): `--styled-output` actually styles, it is not parse-only. BL-489 parses and stores the switch. curl: "Enables styled output. When this option is enabled, curl uses colors and other styling in its output to make it more readable." (https://curl.se/docs/manpage.html, checked 2026-09-28).
- The reference for the escape sequences and the conditions (terminal detection, which header output is styled, the hyperlink rule, Windows virtual-terminal handling) is curl's `src/tool_cb_hdr.c` and `src/tool_setup.h`/`tool_main.c` at tag `curl-8_21_0`; confirm the bytes by running the reference curl under a terminal that records output (for example `script -q -c "curl -i http://127.0.0.1:<P>/" out.txt` on Linux or macOS, with `Record-CurlExchange.ps1 -NoServer` or its loopback server) and copy them into Notes.
- Terminal detection goes through a seam (`Console.IsOutputRedirected` in production) so tests need no terminal. Code: `Curl.Output.UnitLibrary` (header writers), `Curl.Console` (standard output and the terminal check).

## Acceptance criteria

- [x] Measured first as above; the styled bytes for `-i` against a `200` and against a `301` with `Location` copied into Notes.
- [x] `Curl.Output.UnitTests` pin the styled header bytes for both cases and unstyled bytes with `--no-styled-output`; `Curl.Console.UnitTests` show styling only when the terminal seam says standard output is a terminal.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- Measured 2026-09-29 with curl 8.18.0 (OpenSSL build, WSL Ubuntu; the nearest Linux curl on
  this machine, and `tool_cb_hdr.c`'s styling is unchanged from 8.18 to 8.21) under
  `script -q -c "curl -si http://127.0.0.1:8099/a"`, the server a canned `nc -l` reply. A
  Windows console cannot be recorded through a pipe, so the Windows bytes are the source's
  (`BOLDOFF` `ESC[22m`, no `LINK`). The pty turns each `\n` into `\r\n`; raw bytes below.
  - `200`: `HTTP/1.1 200 OK\r\n` `ESC[1mContent-TypeESC[0m: text/plain\r\n`
    `ESC[1mContent-LengthESC[0m: 3\r\n` `\r\n` `hi\n`.
  - `301` with `Location: /next`: `HTTP/1.1 301 Moved Permanently\r\n`
    `ESC[1mLocationESC[0m: ESC]8;;http://127.0.0.1:8099/nextESC\/next\r\nESC]8;;ESC\`
    `ESC[1mContent-LengthESC[0m: 0\r\n` `\r\n`.
  - `Location:  mailto:x@y`: name bolded, value written as it is (no link for that scheme).
  - `-s -D - -o /dev/null`: no styling at all; curl styles only `-i`/`-I` lines.
- Plan: `Curl.Output` gets `StyledHeaderLines` (one line's bytes) and `StyledHeaderStream`;
  `Curl.Console` decides when (`CurlCommandRunner.HeaderStylesFor`) and wraps only the body
  side in `TransferContextFactory`; on Windows `StandardOutputVirtualTerminal` turns on
  virtual-terminal processing as curl does and restores the mode. Decisions in ADR-0246,
  which is why its file was added to `touches` (no task in Doing names it).
- `--no-styled-output` is a runner decision, so its unstyled bytes are pinned in
  `Curl.Console.UnitTests` (`CurlCommandRunnerStyledOutputTests.RunAsync_NoStyledOutput_*`);
  `Curl.Output.UnitTests` pin the styled bytes of both measured cases, Windows' bytes, and
  every link rule.
- Choices taken: the link target is resolved with `Uri` (BCL) rather than a port of curl's
  URL API, so only the hyperlink target of an unusual `Location` could differ; the text shown
  is always the received value. `--ai-help` needs no change: the option set is unchanged.
- Measure-CodeQuality 2026-09-29: `Curl.Output.UnitLibrary` and `Curl.Console` both 100% line
  and branch, 0 failing members.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. -i/-I header lines on a terminal are styled as curl does under --styled-output (bold names, OSC 8 Location links off Windows); redirected, -o, -D and --no-styled-output output unchanged
