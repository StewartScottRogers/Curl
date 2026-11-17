---
id: BL-5350
title: Redact user info in a scheme-less --har and --record URL without throwing on a one-character URL
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extension.Har.UnitLibrary/HarTransferHook.cs, Curl.Extension.RecordReplay.UnitLibrary/RecordReplayTransferHook.cs, Curl.Extension.Har.UnitTests, Curl.Extension.RecordReplay.UnitTests]
requirement: none
created: 2026-11-17
completed:
---
# BL-5350 — Redact user info in a scheme-less --har and --record URL without throwing on a one-character URL

## Goal

`--har` and `--record` never write a URL's user and password, even for a scheme-less URL, and never throw on a short URL.

## Context

Found by the shift-end review of 2026-11-16 (type-design-analyzer, confirmed by hand) in commit 6fd5bf49aa (`fix(har,record): redact URL user-info password`).

`Redacted(string url)` is copied into `Curl.Extension.Har.UnitLibrary/HarTransferHook.cs:24` and `Curl.Extension.RecordReplay.UnitLibrary/RecordReplayTransferHook.cs:23`. It is given `context.Url.OriginalString`, the URL as typed. It has two defects:

- **Crash:** with no `://`, `authorityStart` is `-1 + 3 = 2`. For a URL of length 0 or 1 (e.g. `curl --record dir x`), `url.IndexOfAny([...], 2)` throws `ArgumentOutOfRangeException` instead of running the transfer.
- **Leak:** a scheme-less URL with credentials, such as `user:secret@example.com/`, hits `authorityStart < 3` and is returned unchanged. The password is written to the HAR file or the recorded exchange, which is the leak the original fix meant to close.

`HttpExchangeLog.WithoutUserInfo` in `Curl.Original.Protocol.Http.UnitLibrary` is bounds-safe and can serve as a reference. The extensions must not reference it (`VerifyExtensionIsolation`).

## Acceptance criteria

- [ ] `Redacted("x")` and `Redacted("")` return their input without throwing, in both hooks.
- [ ] `Redacted("user:secret@example.com/path")` returns a string that does not contain `secret` and that holds `REDACTED:REDACTED@example.com/path`, in both hooks.
- [ ] `Redacted("example.com/?u=http://a@b/")` does not change the query, in both hooks.
- [ ] Each case has a unit test in `Curl.Extension.Har.UnitTests` and `Curl.Extension.RecordReplay.UnitTests`.
- [ ] Both libraries still meet the four quality gates.

## Notes

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing. Criteria at claim: 6cb4cc4d 71cdac43 cdebcb97 d8c26815 00faa597.
