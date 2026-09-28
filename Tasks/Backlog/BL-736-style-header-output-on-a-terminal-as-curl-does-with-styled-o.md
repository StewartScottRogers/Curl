---
id: BL-736
title: Style header output on a terminal as curl does with --styled-output
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-489]
touches: [Curl.Output.UnitLibrary, Curl.Output.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-736 — Style header output on a terminal as curl does with --styled-output

## Goal

When standard output is a terminal and `--styled-output` is on (the default), header output from `-i`, `-I` and `-D -` is styled exactly as curl 8.21.0 styles it (bold header names, and the `Location:` value as an OSC 8 hyperlink where curl writes one), `--no-styled-output` turns it off, and redirected output is never styled, on every platform.

## Context

- Conformance audit 2026-09-28, row 2; standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): `--styled-output` actually styles, it is not parse-only. BL-489 parses and stores the switch. curl: "Enables styled output. When this option is enabled, curl uses colors and other styling in its output to make it more readable." (https://curl.se/docs/manpage.html, checked 2026-09-28).
- The reference for the escape sequences and the conditions (terminal detection, which header output is styled, the hyperlink rule, Windows virtual-terminal handling) is curl's `src/tool_cb_hdr.c` and `src/tool_setup.h`/`tool_main.c` at tag `curl-8_21_0`; confirm the bytes by running the reference curl under a terminal that records output (for example `script -q -c "curl -i http://127.0.0.1:<P>/" out.txt` on Linux or macOS, with `Record-CurlExchange.ps1 -NoServer` or its loopback server) and copy them into Notes.
- Terminal detection goes through a seam (`Console.IsOutputRedirected` in production) so tests need no terminal. Code: `Curl.Output.UnitLibrary` (header writers), `Curl.Console` (standard output and the terminal check).

## Acceptance criteria

- [ ] Measured first as above; the styled bytes for `-i` against a `200` and against a `301` with `Location` copied into Notes.
- [ ] `Curl.Output.UnitTests` pin the styled header bytes for both cases and unstyled bytes with `--no-styled-output`; `Curl.Console.UnitTests` show styling only when the terminal seam says standard output is a terminal.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
