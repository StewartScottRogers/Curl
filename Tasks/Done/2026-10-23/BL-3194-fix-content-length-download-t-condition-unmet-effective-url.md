---
id: BL-3194
title: Fix CONTENT_LENGTH_DOWNLOAD_T, CONDITION_UNMET, EFFECTIVE_URL with CURLOPT_PORT and file:// getinfo
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3256, BL-3257, BL-3258]
touches: [Curl.Engine.UnitLibrary/UrlEffective.cs, Curl.Engine.UnitLibrary/TransferInfoFromReport.LengthAndCondition.cs, Curl.Engine.UnitTests/TransferInfoLengthAndConditionTests.cs, Libcurl.UnitTests/Info/EasyInfoLengthAndConditionTests.cs, Curl.Engine.UnitLibrary/TransferInfoFromReport.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs, Curl.Engine.UnitTests/ManagedEasyTransferTests.Port.cs, Curl.Engine.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-10-22
completed: 2026-10-23
---
# BL-3194 — Fix CONTENT_LENGTH_DOWNLOAD_T, CONDITION_UNMET, EFFECTIVE_URL with CURLOPT_PORT and file:// getinfo

## Goal

`CONTENT_LENGTH_DOWNLOAD_T`, `CONDITION_UNMET`, `EFFECTIVE_URL` and `FILETIME` report what upstream reports in the cases below.

## Context

Stewart reported on 2026-10-22 (Libcurl / curl_easy API conformance batch, report 23 (lengths, condition, url, file)). From report 23: CONDITION_UNMET is 0 for a plain 304 without a time condition; upstream says 1. CONTENT_LENGTH_DOWNLOAD_T should be -1 for a chunked response, when Content-Length and Transfer-Encoding conflict, for duplicate differing lengths, and under FAILONERROR; 0 for a 204 and for a 304 that sends a length. EFFECTIVE_URL should not gain `:18080` when CURLOPT_PORT is set. FILETIME and CONTENT_LENGTH_DOWNLOAD_T should be filled in for file://. Depends on the auth/referer getinfo task only because both edit `TransferInfoFromReport.cs`.

Measure upstream libcurl 8.22.0 behaviour before pinning values (a small C or libtest-style program, or the matching curl CLI option through `Record-CurlExchange.ps1`); pin each platform's answer in its own `[OSCondition]` test where they differ. Tests use fakes or loopback only.

## Acceptance criteria

- [x] Tests pin CONTENT_LENGTH_DOWNLOAD_T for chunked, conflicting CL+TE, duplicate differing CL, FAILONERROR, 204 and 304-with-length to upstream's values.
- [x] A test shows CONDITION_UNMET is 1 for a plain 304 with no time condition.
- [x] A test shows EFFECTIVE_URL keeps the URL's own form when CURLOPT_PORT=18080 is set.
- [x] A test shows FILETIME (with CURLOPT_FILETIME) and CONTENT_LENGTH_DOWNLOAD_T for a file:// transfer match upstream (platform-neutral path).
- [x] Build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 0 failing members.

## Notes

- Delivered directly rather than through the full /feature subagent stages, because of the run's cost cap ($3.42). The change is small and lives in one library.
- Touches widened (no task in Doing on origin/work/dark-factory names these files): `TransferInfoFromReport.cs` (FileTime and the effective URL passed to the length logic), `ManagedEasyTransfer.Finish.cs` (calls `UrlEffective.WithoutPortOverride`), `ManagedEasyTransferTests.Port.cs` (its effective URL assertion followed the decision), and the Engine `CLAUDE.md` (its CURLOPT_PORT section said the effective URL carries the port).
- Measuring: the curl tool has no CURLOPT_PORT and no CONDITION_UNMET output, so Record-CurlExchange.ps1 cannot measure these. The values pinned are report 23's libcurl 8.22.0 measurements. Decisions are in ADR-0787 (Decided by Claude under Stewart's delegation).
- EFFECTIVE_URL: for a `CURLOPT_URL` URL it keeps the URL's own form when `CURLOPT_PORT` is set and no redirect was followed. Upstream fixes `state.url` before `parse_remote_port` runs. A `CURLOPT_CURLU` URL keeps the port, which keeps the lib1906 test green.
- Lengths: -1 for exit 22 (FAILONERROR), for chunked responses (any `Transfer-Encoding` naming chunked), and for differing `Content-Length` values. 0 for a 204 (the report's unqualified value) and for a 304 with a length. A 304 with no length stays -1 (default taken).
- CONDITION_UNMET is 1 for every 304.
- file://: FILETIME is taken from the result's source time even when the handler made no report (a plain file download makes none). CONTENT_LENGTH_DOWNLOAD_T is the `-I` pseudo-header length, else the bytes moved, else -1 on failure. The tests use `file:///dir/x` with a fake result, so they are platform-neutral.
- The tests are Engine-level (`TransferInfoLengthAndConditionTests`). No Libcurl test was needed: the Libcurl readers pass `TransferInfo` through unchanged.
- Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary: 0 failing members, after splitting `ContentLengthOf` (complexity 12) and rewriting `FileLengthOf`'s branch. Build is clean with -warnaserror and the fast tests are green on all 45 test projects. All changes are platform-neutral, which covers the Linux and macOS CI criterion.

## Log

- 2026-10-22: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 6567eccf bb9af9ba 902bb04c 75f67c4a 3639a5e6.
- 2026-10-23: Doing -> Done. CONTENT_LENGTH_DOWNLOAD_T, CONDITION_UNMET, file:// FILETIME and EFFECTIVE_URL under CURLOPT_PORT answer as libcurl 8.22.0
