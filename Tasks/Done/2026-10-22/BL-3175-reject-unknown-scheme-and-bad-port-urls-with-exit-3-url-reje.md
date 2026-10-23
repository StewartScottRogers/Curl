---
id: BL-3175
title: Reject unknown-scheme and bad-port URLs with exit 3 URL rejected instead of connecting
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary/UrlSchemeGuesser.cs, Curl.Protocol.Abstractions.UnitLibrary/CurlUrlParser.cs, Curl.Core.UnitTests/UrlSchemeGuesserRejectionTests.cs, Curl.Console.UnitTests/CurlCommandRunnerUrlRejectionPortTests.cs, Curl.Core.UnitTests/CoreParserAdversarialTests.cs]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3175 — Reject unknown-scheme and bad-port URLs with exit 3 URL rejected instead of connecting

## Goal

An unknown scheme or odd URL is rejected with curl's exit 3 `URL rejected` message instead of Curl trying to connect.

## Context

Stewart reported on 2026-10-22; measured the same day with the preview build and System32 curl 8.21.0 Schannel: for `h://127.0.0.1:9/x`, `http:` and `1http://127.0.0.1:9/`, real curl exits 3 with `curl: (3) URL rejected: Port number was not a decimal number between 0 and 65535`; Curl tries to connect (exit 28 timeout in the coordinator's run; Stewart saw exit 6 `Could not resolve host: h`). `ht tp://...` already matches (exit 3, Malformed input) and must stay as it is. The scheme-less URL guess in `Curl.Core.UnitLibrary/UrlSchemeGuesser.cs` (`AddGuessedScheme`, `HasScheme`) must validate the port the way curl's URL parser does and reject the same way (the rejection texts are in `Curl.Protocol.Abstractions.UnitLibrary/CurlUrlRejectionMessages.cs`, the parser in `CurlUrlParser.cs`). Measure curl 8.22.0 with `Record-CurlExchange.ps1` for at least: the three repros, `h://x/`, `1http://x/`, `http:`, `http://`, `://x`, and `ht tp://x/`, and pin the exact text and exit code of each. The `%{url_effective}` task shares `CurlUrlParser.cs` and `UrlSchemeGuesser.cs`; the board serialises them.

## Acceptance criteria

- [x] Notes record curl 8.22.0's exit code and standard-error text for each measured URL.
- [x] `Curl.Core.UnitTests/UrlSchemeGuesserRejectionTests.cs` has tests for `h://127.0.0.1:9/x`, `http:` and `1http://127.0.0.1:9/` expecting the rejection message above, with no connect attempt (a fake connector that fails the test if used).
- [x] A test through the command runner (`Curl.Console.UnitTests/CurlCommandRunnerUrlRejectionPortTests.cs`) expects exit 3 and the exact `curl: (3) URL rejected: ...` line for the three repros, and exit 3 `Malformed input to a URL function` (or 8.22.0's text) for `ht tp://127.0.0.1:9/` unchanged.
- [x] The existing scheme-guessing tests (`UrlSchemeGuesserTests.cs`) still pass unchanged.
- [x] `dotnet build Curl.Core.UnitLibrary Curl.Protocol.Abstractions.UnitLibrary -warnaserror` is clean and the fast tests of `Curl.Core.UnitTests`, `Curl.Protocol.Abstractions.UnitTests` and `Curl.Console.UnitTests` pass.

## Notes

- Measured 2026-10-22 with `Record-CurlExchange.ps1 -NoServer` and the reference curl 8.22.0
  (x86_64-w64-mingw32, Schannel), `-sS --max-time 5 <url>`:
  - `h://127.0.0.1:9/x`, `http:`, `1http://127.0.0.1:9/`, `h://x/`, `1http://x/`: exit 3,
    `curl: (3) URL rejected: Port number was not a decimal number between 0 and 65535`.
  - `http://`, `://x`: exit 3, `curl: (3) URL rejected: No host present`.
  - `ht tp://x/`, `ht tp://127.0.0.1:9/`: exit 3, `curl: (3) URL rejected: Malformed input to a URL function`.
  - Extra probes for the rule: `127.0.0.1:/` and `127.0.0.1:` exit 3 (bad port), while
    `http://127.0.0.1:/` connects to port 80 (exit 7) and `http://h:` resolves `h` (exit 6). So an
    empty port is rejected only when the scheme was not in the URL (`Curl_parse_port`'s `has_scheme`).
- Decision (ADR-0767): `UrlSchemeGuesser.AddScheme` leaves such a URL without a scheme (unless the
  scheme to add is `file`), and the transfer URL parser, which already rejects an empty port with no
  scheme, reports it; `CurlUrlParser.cs` and the runner needed no change. `h://` is a drive prefix
  only on Windows, so its tests carry `[OSCondition(OperatingSystems.Windows)]`; elsewhere `h` is a
  scheme and curl answers otherwise (not pinned here).
- `CoreParserAdversarialTests` pinned `1http://x` -> `http://1http://x`, which curl rejects; its row
  became `1http:9//x` (port not empty). No task in Doing on origin/work/dark-factory touches that file,
  so it was added to `touches`.
- The Core tests check the guesser and the parser together (no connector exists at that layer); the
  "no connect attempt" check is the runner test's recording handler, asserted never called.
- `Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary`: 0 failing members.
- CI on Linux and macOS: the tests are platform-neutral (the drive-prefix cases are Windows-only).

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: d303a9d8 a559ec65 9ac6387e 2d4aaa8e 23f252f2.
- 2026-10-22: Doing -> Done. A URL without a scheme whose authority ends in an empty port (http:, 1http://..., h:// on Windows) is rejected with exit 3 bad port, as curl 8.22.0 does
