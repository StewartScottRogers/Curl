---
id: BL-324
title: Report curl's own reason when CurlUrl rejects a URL
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-294]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Console, Curl.Console.UnitTests, Documentation/Planning/Decisions/ADR-0010-representing-urls-system-uri-cannot-round-trip.md]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-324 — Report curl's own reason when CurlUrl rejects a URL

## Goal

When `CurlUrl` rejects a transfer URL, the exit-3 line names the reason curl 8.21.0
names (its `CURLUcode` message) instead of always `Malformed input to a URL function`.

## Context

- BL-294 switched `CurlCommandRunner` to `CurlUrl.TryParse`, which answers only
  true or false, so every rejection prints `curl: (3) URL rejected: Malformed input to a
  URL function` (ADR-0010, "Switch decisions taken under BL-294").
- Measured on 2026-09-27 against curl 8.21.0 (`/mingw64/bin/curl`):
  `curl -sS file://example.com/x` and `curl -sS "file://[::1]/x"` print
  `curl: (3) URL rejected: Bad file:// URL`; `curl -sS "http:////h/"` prints
  `curl: (3) URL rejected: Unsupported number of slashes following scheme`.
  Before BL-294 the `file` handler printed `Bad file:// URL` for the first two.
- Messages are libcurl's `curl_url_strerror` texts (`lib/urlapi.c`, `lib/strerror.c`
  at 8.21.0). Measure each rejection class before pinning its text.
- Start at `CurlUrlParser` (each `return null`), `CurlUrl.TryParse`, and
  `CurlCommandRunner`'s `MalformedUrlMessage`.

## Acceptance criteria

- [x] `CurlUrl` exposes why it rejected a text (for example a `TryParse` overload with an `out` reason), without changing the existing `TryParse(string, bool, out CurlUrl)`.
- [x] `curl -sS file://example.com/x` run through `CurlCommandRunner` exits 3 with `curl: (3) URL rejected: Bad file:// URL`, pinned by a test in `Curl.Console.UnitTests`.
- [x] `http:////h/` exits 3 with `curl: (3) URL rejected: Unsupported number of slashes following scheme`, pinned by a test.
- [x] Every other rejection class `CurlUrlParser` has is measured against curl 8.21.0 and pinned, or keeps `Malformed input to a URL function` where curl prints that.
- [x] `dotnet build` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage for the touched libraries.

## Notes

- Filed by BL-294.
- Plan: a public `CurlUrlRejection` enum (one value per `CURLUcode` class, `None` when
  accepted) set by a new `CurlUrl.TryParse(text, pathAsIs, out url, out rejection)`;
  `CurlUrlRejectionMessages.ToCurlMessage()` gives curl's `curl_url_strerror` text.
  `CurlUrlParser`, `CurlUrlAuthority` and `CurlUrlHost` pass the reason out instead of a
  bare null/false. `CurlCommandRunner` prints `URL rejected: ` + the message;
  `MalformedUrlMessage` became `UrlRejectedPrefix`.
- Measured 2026-09-27 with `/mingw64/bin/curl -gsS --max-time 1 <url>` (curl 8.21.0
  Schannel, mingw). Pinned in `CurlUrlRejectionTests` and `CurlCommandRunnerTests`:
  - Malformed input: `http://a b/`, `http://h/a b`.
  - Unsupported number of slashes: `http:////h/`, `http:////`.
  - No host part: `http:/`, `http://`, `http:///`, `http://?x`, `http://@/`, `http://u@/`,
    `http://u:p@/`, `http://:80/`, `http://u@:80/`, `:80/x`.
  - Port number: `http://h:99999/`, `http://h:1x/`, `http://h:-1/`, `http://h:+1/`,
    `http://:x/`, `http://[::1]x/`, `http://[::1]]/`, `http://[::1]:x/`, `h:/x`, `h:`,
    `http:`, `http:x`, `x:///`.
  - Bad IPv6: `http://[::1/`, `http://[::1`, `http://[::g]/`, `http://[::1%]/`,
    `http://[::1%25]/`, `http://[:::1]/`, `http://[1.2.3]/`, `http://[]/`.
  - Bad hostname: `http://[::1%1234567890123456]/` (zone id over 15), `http://a%01b/`,
    `http://a%00b/`, `http://a!b/`, `http://%/`, `http://a%zz/`, `http://./`,
    `http://../`, `http://a../`, `a!b`.
  - Bad file:// URL: `file://example.com/x`, `file://[::1]/x`, `file://localhost`, `file://a`.
  - Accepted (not rejections): `http://[::1%zz]/`, `http://h:/x`, `http://[::1]:`.
  - Not measurable here: the 8,000,000-byte limit (Windows command lines stop far
    short) and drive letters outside Windows; both follow `lib/urlapi.c` at 8.21.0
    (`CURLUE_MALFORMED_INPUT`, `CURLUE_BAD_FILE_URL`).
- Parser change: `http://` and `http:///` (all slashes, nothing after) used to be
  rejected as too many slashes internally; they now fall through to "No host", as curl
  says. Acceptance is unchanged for every URL.
- Decision recorded in ADR-0010, "Rejection reasons decided under BL-324" (decided by
  Claude under Stewart's delegation). ADR-0010 was added to `touches` for it: no task in
  Doing names it (the others touch Curl.Cli and Curl.Networking).
- Coverage: `Measure-CodeQuality.ps1` (fast run) reports Curl.Protocol.Abstractions.UnitLibrary
  100% line, 100% branch, 0 failing, worst CRAP 8. Curl.Console's only failing member is
  `DiskWriteOutFileOpener.TryOpen`, untouched here and covered only by Integration tests
  (as BL-280, BL-311 and BL-328 record); every member this task changed is covered.
- Results: `dotnet build` clean (0 warnings); fast tests green (Abstractions 438,
  Console 644, no failures in any project); `dotnet format --verify-no-changes` clean on
  all four projects.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. CurlUrl says why it rejects a URL and the exit-3 line prints curl 8.21.0's own reason
