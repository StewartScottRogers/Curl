---
id: BL-208
title: Retry transient transfer failures on TimeProvider
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-160]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-208 — Retry transient transfer failures on TimeProvider

## Goal

A retry policy re-runs a transfer for curl's transient failures with curl's backoff and warning line, on the injected `TimeProvider`.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item K6. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Transient: timeout 28 and HTTP 408, 429, 500, 502, 503, 504; `Retry-After` honoured.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] Which failures retry, the backoff sequence and `Retry-After` handling are measured on curl 8.21.0 and pinned on `FakeTimeProvider`.
- [x] The warning `Warning: Problem : ... Will retry in N seconds. M retries left.` matches the measured text (measured: `Retrying in N seconds.`; see Notes).
- [x] `dotnet build Curl.Core.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Core`.

## Notes

- Plan item: K6 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Delivered: `Curl.Core.TransferRetrier` (wraps any `Func<ITransferContext, ValueTask<TransferResult>>`, e.g. `RedirectFollower`), `RetryPolicy` (`--retry`, `--retry-delay`), `RetryAfterHeader`, `TransferRetryReason`, `TransferRetryWarning`. Tests: `TransferRetrierTests`, `RetryAfterHeaderTests`, `TransferRetryWarningTests`, `RetryPolicyTests`, on a new `Curl.Core.UnitTests/Fakes/FakeTimeProvider` (each timer moves the clock to its due time and records it). Curl.Core: 661 passed, 2 skipped (pre-existing); `Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary`: 100% line, 100% branch, 0 failing members, worst CRAP 10.
- Measured 2026-09-26 with curl 8.21.0 (`/mingw64/bin/curl`, mingw, Schannel) against a Python loopback server (`/s/<status>[/<Retry-After>]`, `/once/...` sends Retry-After on the first request only, `/hang` never answers) that logged each request's time. `Record-CurlExchange.ps1` records one exchange, not request timing, so it was not used.
  - `curl --retry 1 --retry-delay 0 -sS http://127.0.0.1:18208/s/<code>`: 408, 429, 500, 502, 503, 504, 522, 524 were requested twice and the body printed twice, exit 0; 403, 404, 501 once. `-sS` printed no warning.
  - `curl --retry 4 -o /dev/null http://127.0.0.1:18208/s/503`: requests at +0, +1, +3, +7, +15 s; stderr `Warning: Problem : HTTP error. Retrying in 1 second. 4 retries left.`, `... Retrying in 2 seconds. 3 retries left.`, `... 4 seconds. 2 retries left.`, `... 8 seconds. 1 retry left.`, exit 0. The text is "Retrying in", not the "Will retry in" this task's criterion guessed; the measured text is pinned.
  - `--retry 3 --retry-delay 3`: 3, 3, 3 s. `--retry 2 --retry-delay 1.5`: `Retrying in 1.500 seconds.` twice. `--retry-delay 0` means the default backoff.
  - Retry-After: `3` with `--retry 3` waited 3, 3, 3 (replaces the backoff, not a max); `1` with `--retry 4` waited 1, 1, 1, 1; `/once/503/5` waited 5, 1, 2 (a Retry-After wait does not advance the backoff); `0` waited 1, 2; `3` with `--retry-delay 1` waited 3; `1` with `--retry-delay 3` waited 1; `2` on a 500 waited 2; an RFC 1123 date 5 s ahead waited 5; a past date, `abc`, `garbage 3`, `-3` and `99999999999999999999` fell back to 1; `3abc` and ` 3` gave 3, `2.5` gave 2; `21601` and `9999999` gave 21600, `21599` gave 21599.
  - `--retry 2 -m 1 http://.../hang`: `curl: (28) Operation timed out after 1008 milliseconds with 0 bytes received`, then `Warning: Problem : timeout. Retrying in 1 second. 2 retries left.`, ..., `Retrying in 2 seconds. 1 retry left.`, final exit 28. `--retry 1 http://nonexistent.invalid/`: exit 6 retried with `: timeout`. `--retry 1 http://127.0.0.1:1/` (exit 7) not retried. `--retry 1 -f .../s/503`: `curl: (22) The requested URL returned error: 503`, warning, retried, exit 22.
  - `--retry 2147483647` on a 503 with `Retry-After: 9999999` printed `Warning: Problem : HTTP error. Retrying in 21600 seconds. 2147483647 retries ` / `Warning: left.`: wrapped at 79 columns, which `Curl.Console`'s `WarningLineWrapper` already does, so `TransferRetryWarning` returns the line unwrapped.
- The 10-minute backoff cap and 522/524 come from upstream `src/tool_operate.c`/`src/tool_main.h` (`RETRY_SLEEP_MAX 600000L`); 522 and 524 were then measured. The cap itself was not waited out (it takes 17 minutes); it is a number, not output text.
- Decision (sensible default, recorded here because `Documentation/Planning/Decisions` is held by BL-303 in Doing; BL-318 writes the ADR): `RetryAfterHeader` reads the three RFC 9110 HTTP-date forms with `DateTimeOffset.TryParseExact` rather than porting libcurl's lenient `parsedate.c`. BCL only, and every conforming server sends one of the three.
- Scope decision: `--retry-max-time`, `--retry-all-errors`, `--retry-connrefused` and FTP 4xx retries are left to BL-317; wiring into `Curl.Console` (error line before each warning, `-s` silencing, output truncation) is BL-241, whose Notes now carry the measurements above.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. TransferRetrier retries curl 8.21.0's transient failures with its backoff, Retry-After and warning line on the injected TimeProvider
