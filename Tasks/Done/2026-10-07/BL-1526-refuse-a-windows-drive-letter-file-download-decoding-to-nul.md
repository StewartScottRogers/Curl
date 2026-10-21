---
id: BL-1526
title: Refuse a Windows drive-letter file:// download decoding to NUL with curl's URL rejected: Malformed input text
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: FR-019
created: 2026-10-07
completed: 2026-10-07
---
# BL-1526 — Refuse a Windows drive-letter file:// download decoding to NUL with curl's URL rejected: Malformed input text

## Goal

On Windows, a download of a drive-letter `file://` URL whose path decodes to a NUL (`file:///C:/dir/f.txt%00x`) fails as curl 8.21.0 does: `* URL rejected: Malformed input to a URL function` under `-v`, then `curl: (3) URL rejected: Malformed input to a URL function`.

## Context

- Measured 2026-10-07 on Windows with curl 8.21.0 (Schannel and MinGW builds), BL-1451, ADR-0416: `curl -sSv "file:///C:/<dir>/f.txt%00x"` writes `* URL rejected: Malformed input to a URL function` and `curl: (3) URL rejected: Malformed input to a URL function`. The drive-less form and every upload (`-T f.txt file:///C:/<dir>/up%00x` included) give `curl: (3) URL using bad/illegal format or missing URL` with no `*` line, which BL-1451 implemented in `FileProtocolHandler`.
- The difference comes from upstream URL parsing (`lib/urlapi.c` drive-letter handling under `DOS_FILESYSTEM`, or `lib/url.c` decoding the path for a Windows drive) before `file_connect` runs; find the exact line at tag `curl-8_21_0` and why the upload differs before implementing.
- Curl today: `CurlUrl` (Curl.Protocol.Abstractions.UnitLibrary) accepts the URL, and `FileProtocolHandler` refuses it with the drive-less text. The fix likely belongs where the transfer rejects a URL (`CurlUrlRejection` / `CurlUrlRejectionMessages`), marked Windows-only.

## Acceptance criteria

- [x] (Superseded by measurement, ADR-0418: curl gives the drive-less text, so Curl already matches; pinned platform-neutrally in `CurlUrlRejectionTests` instead.) A test marked `[OSCondition(OperatingSystems.Windows)]` pins the download of `file:///C:/dir/f.txt%00x` failing with exit 3, `URL rejected: Malformed input to a URL function` and its `*` line, before anything opens.
- [x] An upload to `file:///C:/dir/up%00x` and drive-less URLs keep BL-1451's `URL using bad/illegal format or missing URL` with no `*` line.
- [x] `dotnet build` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library` on every changed library reports no failing member.

## Notes

- Re-measured with curl 8.21.0 Schannel on Windows: `curl -sSv "file:///C:/bl1526tmp/f.txt%00x"` writes only `curl: (3) URL using bad/illegal format or missing URL` - the drive-less text. BL-1451's `URL rejected: Malformed input to a URL function` came from the space in `C:\Users\Stewart Rogers\...`: `Curl_junkscan` (lib/urlapi.c:243) refuses it and `parseurlandfillconn` (lib/url.c:2255) prints `URL rejected`. Neither urlapi's drive-letter code nor url.c decodes the path. ADR-0418 records it and corrects ADR-0416.
- No production change: `CurlUrl` already accepts the URL and `FileProtocolHandler` refuses it with BL-1451's text. Two platform-neutral tests in `CurlUrlRejectionTests` pin that the parser accepts the drive-letter `%00` URL and refuses a space in a drive-letter path with `MalformedInput`. The Windows-only test the first criterion asked for would pin a behaviour curl does not have.
- Measure-CodeQuality not run: no production library changed (only a test file), so its metrics cannot move.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Measured: a drive-letter file:// download decoding to NUL already gets curl's file_connect text; parser behaviour pinned, ADR-0418 corrects ADR-0416
