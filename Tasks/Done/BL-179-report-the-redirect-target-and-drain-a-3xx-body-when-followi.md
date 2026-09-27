---
id: BL-179
title: Report the redirect target and drain a 3xx body when following
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-173]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-179 — Report the redirect target and drain a 3xx body when following

## Goal

For a 3xx the handler resolves `Location` against the request URL into `TransferReport.RedirectUrl`, and with `FollowRedirects` reads and discards the 3xx body while still writing its headers.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item H11. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- BL-203 (redirect follower in Core) uses `RedirectUrl`; `-w %{redirect_url}` reads it without `-L` too.

## Acceptance criteria

- [x] Relative, absolute and scheme-relative `Location` values resolve against the request URL into `Report.RedirectUrl`, with or without `FollowRedirects`.
- [x] With `FollowRedirects`, the 3xx body is read and discarded (Content-Length and chunked), and its headers still go to `HeaderOutput`.
- [x] `dotnet build Curl.Protocol.Http.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http`. Tests use the fakes in `Curl.Protocol.Http.UnitTests/Fakes` (added by BL-169), never a socket; every parser test also runs with 1-byte chunks.

## Notes

- Plan item: H11 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- 2026-09-26 (lane 2): delivered directly rather than through the full agent pipeline: one new internal class and a two-line handler change, planned, measured and tested in-session.
- Measured on curl 8.21.0 (Schannel, Windows) with a raw socket server and `-w '%{redirect_url}'`, request `http://127.0.0.1:PORT/a/b/c?q=1#base`; every value is pinned in `HttpRedirectLocationTests`:
  - Only a 3xx status gives a redirect URL (curl's `http.c` takes `Location` for 300-399 only); the first `Location` with a non-empty value wins.
  - A relative value is resolved by RFC 3986 5.2, after its spaces are encoded (`/a b` -> `/a%20b`) and its percent escapes uppercased (`/%7e` -> `/%7E`, in path, query and fragment). The base fragment is dropped; `#f` keeps the base query.
  - An absolute value keeps its bytes as sent (`http://h/y z`, `http://h/%7e`, `https://h/%zz`, `http://h:80/x`), with its scheme lowercased (`HTTP://UP.EXAMPLE/Q` -> `http://UP.EXAMPLE/Q`), dot segments removed and an empty path made `/`. One that does not parse (`http://[bad`) is reported whole, as curl's `FOLLOW_FAKE` mode does. `mailto:` and unknown schemes are kept as sent.
  - Under `-L --max-redirs 0` curl writes the 3xx head and chunked trailers to `-D`, writes no body byte to stdout, and reports `%{size_download}` 5 for the 5-byte drained body, so the drained body is counted in `DownloadSize`.
- Choices (sensible defaults, rule 1): non-ASCII in a relative `Location` is percent-encoded as UTF-8, as curl's URL encoder does, but was not measured byte for byte (Git Bash mangles non-ASCII arguments). curl also percent-decodes an absolute URL's host (`http://H%41/x` -> `http://HA/x`); not modelled, since no real server sends it. The base URL is `ITransferContext.Url` as `System.Uri` normalized it.
- The drain covers only a response with a redirect URL: a 3xx without `Location`, or a `Location` on a 200, still writes its body under `-L`, as curl does (it ignores the body only once it has a URL to follow).
- Follow-up filed: BL-271 (`RedirectFollower` throws on a redirect URL that does not parse; curl exits 1).
- Verified: `dotnet build -warnaserror` clean; fast tests green (Http 446); `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary`: 100% line, 100% branch, 187 members, 0 failing, worst CRAP 10.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. HttpProtocolHandler reports a 3xx Location resolved as curl 8.21.0 does in RedirectUrl, and under -L drains the 3xx body while still writing its headers
