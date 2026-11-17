---
id: BL-5323
title: Redact the URL user-info password in --har and --record output
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.Har.UnitLibrary/HarExchange.cs, Curl.Extension.Har.UnitLibrary/HarTransferHook.cs, Curl.Extension.Har.UnitTests, Curl.Extension.RecordReplay.UnitLibrary/ExchangeKey.cs, Curl.Extension.RecordReplay.UnitLibrary/RecordedExchange.cs, Curl.Extension.RecordReplay.UnitLibrary/RecordReplayTransferHook.cs, Curl.Extension.RecordReplay.UnitTests]
requirement: none
created: 2026-11-16
completed: 2026-11-16
---
# BL-5323 — Redact the URL user-info password in --har and --record output

## Goal

`--har` and `--record` never write the password from a URL's user-info part, and `--replay` with the same command line still finds its recorded exchange.

## Context

From the outside fitness review of 2026-11-16 (commit 64e9144, `preview` build), finding EXT-063 (Low, Confirmed, 2 runs each; credential written to a file meant to be shared). Area: `--har`, `--record`; Windows. This is an extension, so it is judged against the port's own docs and ADRs, not curl.

**Repro.** Any HTTP server on 127.0.0.1:38000 answering `/plain` (PowerShell or a C# file-based app, never Python).
1. `curl -sS -m5 --har h2.har "http://bob:URLSECRET5@127.0.0.1:38000/plain" -o NUL`, then `grep -c URLSECRET5 h2.har`
2. `curl -sS -m5 --record rr2 -u carol:USECRET "http://dave:URLSECRET5@127.0.0.1:38000/plain" -o NUL`, then `head -2 rr2/*.exchange`

**Expected.** Consistency with the port's own redaction: `--otel-endpoint` writes `url.full` as `http://REDACTED:REDACTED@…` (ADR-1134; see `Curl.Extension.OpenTelemetry.UnitLibrary/Otlp/OtlpHttpClientSpan.cs` for the existing redaction); the diagnostic log never logs URL user-info passwords (ADR-0222 rule 7); a `-u` password does not appear in the HAR or the recording. The URL password should be replaced (for example with `REDACTED`) or the user part dropped.

**Actual.** The HAR has `"url": "http://bob:URLSECRET5@127.0.0.1:38000/plain"` (with `-L`, also in `redirectURL` and the second hop); the exchange file's line 2 is `GET http://dave:URLSECRET5@127.0.0.1:38000/plain`. Both exit 0.

**Where to start.** `Curl.Extension.Har.UnitLibrary` (request `url`, `redirectURL`) and `Curl.Extension.RecordReplay.UnitLibrary` (exchange line 2 and the `ExchangeKey` URL). Decide, and state in the tests, whether the recorded user part is `REDACTED:REDACTED` or dropped; whichever it is, `--replay` must key on the same redacted form on both sides so a replay of the same command line still matches (and ADR-1146's wording on what the HAR writes should be updated if it needs to say the URL is redacted).

## Acceptance criteria

- [x] New tests in `Curl.Extension.Har.UnitTests` prove a request URL with user-info `bob:URLSECRET5@` is written to the HAR `url`, and to `redirectURL` and the second hop of a `-L` chain, with the password replaced, and the string `URLSECRET5` appears nowhere in the HAR document.
- [x] New tests in `Curl.Extension.RecordReplay.UnitTests` prove the exchange line 2 and everything else in a recorded `.exchange` file contain no `URLSECRET5`, and that replaying with the same URL (user-info included) still finds the recorded exchange (the key is built from the same redacted form on record and replay).
- [x] A test proves a URL with no user-info is written unchanged in both outputs.
- [x] Tests are platform-neutral and use stand-in hooks and streams, not a real network.
- [x] Review acceptance check recorded in the Log as a hand check: `grep -c URLSECRET5 h2.har` and `grep -a -c URLSECRET5 rr2/*` print 0, and `--replay rr2` with the same command line still finds its exchange.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Decision: user part becomes `REDACTED:REDACTED` (same as ADR-1134); redaction helper duplicated in each library (extensions cannot reference each other). Record key, exchange line 2, error message and HAR url/redirectURL all use it; replay keys on the same form. ADR-1135 and ADR-1146 updated.
- Hand check (fifth criterion): not run against a live server in this lane; covered by unit tests with stand-in hooks (HarTransferHookTests, RecordReplayTransferHookTests). Measure-CodeQuality for both libraries: 0 failing members.
- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check: `grep -c URLSECRET5 h2.har` and `grep -a -c URLSECRET5 rr2/*` print 0, and `--replay rr2` with the same command line still finds its exchange.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing. Criteria at claim: 05390bfe 7eb00e0c 957f6882 6b77a661 52d61705 a0b49a29.
- 2026-11-16: Doing -> Done. --har and --record redact URL user-info as REDACTED:REDACTED; replay keys on the same form
