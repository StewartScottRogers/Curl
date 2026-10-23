---
id: BL-3175
title: Reject unknown-scheme and bad-port URLs with exit 3 URL rejected instead of connecting
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary/UrlSchemeGuesser.cs, Curl.Protocol.Abstractions.UnitLibrary/CurlUrlParser.cs, Curl.Core.UnitTests/UrlSchemeGuesserRejectionTests.cs, Curl.Console.UnitTests/CurlCommandRunnerUrlRejectionPortTests.cs]
requirement: none
created: 2026-10-22
completed:
---
# BL-3175 — Reject unknown-scheme and bad-port URLs with exit 3 URL rejected instead of connecting

## Goal

An unknown scheme or odd URL is rejected with curl's exit 3 `URL rejected` message instead of Curl trying to connect.

## Context

Stewart reported on 2026-10-22; measured the same day with the preview build and System32 curl 8.21.0 Schannel: for `h://127.0.0.1:9/x`, `http:` and `1http://127.0.0.1:9/`, real curl exits 3 with `curl: (3) URL rejected: Port number was not a decimal number between 0 and 65535`; Curl tries to connect (exit 28 timeout in the coordinator's run; Stewart saw exit 6 `Could not resolve host: h`). `ht tp://...` already matches (exit 3, Malformed input) and must stay as it is. The scheme-less URL guess in `Curl.Core.UnitLibrary/UrlSchemeGuesser.cs` (`AddGuessedScheme`, `HasScheme`) must validate the port the way curl's URL parser does and reject the same way (the rejection texts are in `Curl.Protocol.Abstractions.UnitLibrary/CurlUrlRejectionMessages.cs`, the parser in `CurlUrlParser.cs`). Measure curl 8.22.0 with `Record-CurlExchange.ps1` for at least: the three repros, `h://x/`, `1http://x/`, `http:`, `http://`, `://x`, and `ht tp://x/`, and pin the exact text and exit code of each. The `%{url_effective}` task shares `CurlUrlParser.cs` and `UrlSchemeGuesser.cs`; the board serialises them.

## Acceptance criteria

- [ ] Notes record curl 8.22.0's exit code and standard-error text for each measured URL.
- [ ] `Curl.Core.UnitTests/UrlSchemeGuesserRejectionTests.cs` has tests for `h://127.0.0.1:9/x`, `http:` and `1http://127.0.0.1:9/` expecting the rejection message above, with no connect attempt (a fake connector that fails the test if used).
- [ ] A test through the command runner (`Curl.Console.UnitTests/CurlCommandRunnerUrlRejectionPortTests.cs`) expects exit 3 and the exact `curl: (3) URL rejected: ...` line for the three repros, and exit 3 `Malformed input to a URL function` (or 8.22.0's text) for `ht tp://127.0.0.1:9/` unchanged.
- [ ] The existing scheme-guessing tests (`UrlSchemeGuesserTests.cs`) still pass unchanged.
- [ ] `dotnet build Curl.Core.UnitLibrary Curl.Protocol.Abstractions.UnitLibrary -warnaserror` is clean and the fast tests of `Curl.Core.UnitTests`, `Curl.Protocol.Abstractions.UnitTests` and `Curl.Console.UnitTests` pass.

## Notes

## Log

- 2026-10-22: Created.
