---
id: BL-523
title: Refuse schemes --proto excludes and redirects --proto-redir excludes
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-522]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-523 — Refuse schemes --proto excludes and redirects --proto-redir excludes

## Goal

A URL whose scheme `--proto` excludes is refused before any connection, and a `-L` redirect to a scheme `--proto-redir` excludes (by default anything but HTTP, HTTPS, FTP and FTPS) is refused, each with the exit code and message curl 8.21.0 gives.

## Context

- Conformance audit 2026-09-28, row 10 (Blocker). Parsing is BL-522.
- Scheme dispatch: `Curl.Core.UnitLibrary/ProtocolDispatcher.cs`; redirects: `RedirectFollower.cs`, `RedirectPolicy.cs`, and `Curl.Console/RedirectPolicyMapping.cs`.
- Whether the default redirect set applies even without `--proto-redir` today (for example a redirect to `file://`) must be measured; if Curl already refuses such redirects, keep one code path.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1`: `--proto =https http://...`, `--proto -http http://...`, `-L` with `Location: file:///dir/x`, `Location: dict://127.0.0.1/x` with and without `--proto-redir =http,dict`; stderr and exit code copied into Notes.
- [x] `Curl.Core.UnitTests` pin the refusal for each measured case; `Curl.Console.UnitTests` pin one of each end to end.
- [x] New tests are platform-neutral (drive-less `file:///dir/x`).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Core.UnitLibrary` and `Curl.Console`.

## Notes

Measured with `Record-CurlExchange.ps1` against the Windows curl 8.21.0 (Schannel), 2026-09-28, port
48523 (stderr lines end CRLF):

| Arguments | Location | Exit | Request sent | stderr |
| --- | --- | ---: | --- | --- |
| `-sS --proto =https http://...` | | 1 | none | `curl: (1) Protocol "http" is disabled` |
| `-sS --proto -http http://...` | | 1 | none | `curl: (1) Protocol "http" is disabled` |
| `-v --proto -http http://...` | | 1 | none | `* Protocol "http" is disabled` then the same `curl: (1)` line (filed as BL-805) |
| `-sS --proto =http ftp://...` / `file:///dir/x` | | 1 | none | `Protocol "ftp"` / `"file" is disabled` |
| `-sS --proto =https HTTP://...` / `127.0.0.1:48523/` | | 1 | none | `Protocol "http" is disabled` (lowercased; guessed scheme checked too) |
| `-sS --proto =http bogus://...` | | 1 | none | `curl: (1) Protocol "bogus" not supported` (unknown wins over disabled) |
| `-sS -L` | `file:///dir/x` | 1 | first only | `curl: (1) Protocol "file" is disabled (in redirect)` |
| `-sS -L` | `dict://127.0.0.1:48523/x` | 1 | first only | `curl: (1) Protocol "dict" is disabled (in redirect)` |
| `-sS -L --proto-redir =http,dict` | `dict://...` | 0 | both (dict request sent) | (none) |
| `-sS -L --proto-redir =http,dict` | `file:///dir/x` | 1 | first only | `Protocol "file" is disabled (in redirect)` |
| `-sS -L --proto =http --proto-redir =http,dict` | `dict://...` | 1 | first only | `Protocol "dict" is disabled (in redirect)` |
| `-sS -L --proto -https` | `https://...` | 1 | first only | `Protocol "https" is disabled (in redirect)` |

So a redirect target must be allowed by `--proto` and by `--proto-redir` (default http, https, ftp,
ftps); the first URL only by `--proto`. The default redirect set was already enforced by
`RedirectFollower`, so it stays one code path: `RedirectPolicyMapping` now fills
`RedirectPolicy.AllowedSchemes` from `--proto-redir` and the new `AllowedTransferSchemes` from `--proto`.

Decisions (Decided by Claude under Stewart's delegation; small enough to record here rather than in an ADR):
- The first-URL check lives in `ProtocolDispatcher.DispatchAsync(context, allowedSchemes)`: the
  dispatcher knows which schemes have a handler, so "not supported" comes before "is disabled", in
  curl's order. A scheme curl knows but Curl has no handler for yet says "not supported", the same
  as today without `--proto`.
- The `--proto` set rides on `RedirectPolicy` (`AllowedTransferSchemes`), which `RedirectFollower`
  already receives for every transfer, with or without `-L`; this avoided threading a new parameter
  through the runner's retry and watchdog methods.
- The `-v` line is follow-up BL-805, not widened into this task.

Gates: `dotnet build Curl.slnx -warnaserror` clean; fast tests green (Core 961 passed, Console 1385
passed); `Measure-CodeQuality.ps1`: Curl.Core.UnitLibrary and Curl.Console 100% line, 100% branch,
0 failing members (worst CRAP 10).

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. --proto refuses an excluded URL scheme before connecting and -L refuses redirects --proto-redir or --proto exclude, with curl 8.21.0's exit 1 and messages
