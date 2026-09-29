---
id: BL-524
title: Use --proto-default as the scheme for a URL without one
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-522]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-524 — Use --proto-default as the scheme for a URL without one

## Goal

With `--proto-default <scheme>`, a URL given without a scheme uses that scheme instead of curl's host-name guessing, as curl 8.21.0 does.

## Context

- Conformance audit 2026-09-28, row 10 (Blocker). Parsing is BL-522.
- Scheme guessing: `Curl.Core.UnitLibrary/UrlSchemeGuesser.cs` (`ftp.` → ftp and so on); wired in `Curl.Console`.
- Measure how it interacts with a host name that would be guessed (`--proto-default https ftp.example`) and with an unknown default.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1`: `--proto-default https 127.0.0.1:<P>/` (with `-k -Tls`), `--proto-default ftp 127.0.0.1:<P>/` (`-Ftp`), `--proto-default dict ftp.localhost` style guessing, with `-w '%{url_effective}'`; stdout, stderr and exit code copied into Notes.
- [x] `Curl.Core.UnitTests` pin each measured case; a `Curl.Console.UnitTests` test pins one end to end.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Core.UnitLibrary` and `Curl.Console`.

## Notes

Measured 2026-09-28 with `Record-CurlExchange.ps1`, curl 8.21.0 (mingw, Schannel), Windows,
each with `-sS -w '%{url_effective}'`:

| Case | stdout | stderr | exit |
| --- | --- | --- | --- |
| `-k --proto-default https 127.0.0.1:18524/` (`-Tls`) | `https://127.0.0.1:18524/` | (empty) | 0 |
| `--proto-default ftp 127.0.0.1:18525/` (`-Ftp`) | `ftp://127.0.0.1:18525/` | (empty) | 0 |
| `--proto-default dict ftp.localhost:1/` | `dict://ftp.localhost:1/` | `curl: (7) Failed to connect to ftp.localhost:1 after 2230 ms: Could not connect to server` | 7 |
| `--proto-default https ftp.localhost:1/` | `https://ftp.localhost:1/` | `curl: (7) Failed to connect to ftp.localhost:1 after … ms: Could not connect to server` | 7 |
| `--proto-default ftp u:p@dict.localhost:1/x` | `ftp://u:p@dict.localhost:1/x` | `curl: (7) Failed to connect to dict.localhost:1 …` | 7 |
| `--proto-default ftp http://127.0.0.1:1/` | `http://127.0.0.1:1/` | `curl: (7) Failed to connect to 127.0.0.1:1 …` | 7 |
| `--proto-default HTTPS 127.0.0.1:1/` (`%{scheme}` too) | `https://127.0.0.1:1/\|https` | `curl: (7) …` | 7 |
| `--proto =http --proto-default https 127.0.0.1:1/` | `https://127.0.0.1:1/` | `curl: (1) Protocol "https" is disabled` | 1 |
| `--proto-default rtmp 127.0.0.1:1/` (unknown) | (empty) | `curl: option --proto-default: a specified protocol is unsupported by libcurl` + try line | 1 |

Findings: the default scheme replaces the host-name guess outright; a URL naming its own
scheme keeps it; `--proto` then applies to the defaulted scheme (BL-523's dispatcher check,
no new code); an unknown default is refused at parse time (BL-522, already pinned).

Implementation: `UrlSchemeGuesser.AddScheme(url, defaultScheme)` in `Curl.Core.UnitLibrary`
(`AddGuessedScheme` now calls it with `null`); `CurlCommandRunner` passes
`options.DefaultProtocol` at all four places a schemeless URL gets its scheme. No ADR: the
behaviour is curl's as measured, with no design choice left open.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. --proto-default gives a URL without a scheme that scheme in place of the host-name guess, as curl 8.21.0 does
