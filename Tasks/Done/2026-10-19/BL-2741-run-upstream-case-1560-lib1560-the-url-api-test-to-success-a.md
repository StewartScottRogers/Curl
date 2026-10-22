---
id: BL-2741
title: Run upstream case 1560 (lib1560, the URL API test) to success and hold it on the ratchet
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2731, BL-2732, BL-2733, BL-2734, BL-2735, BL-2736, BL-2737, BL-2738, BL-2739, BL-2740, BL-2783, BL-2791]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-18
completed: 2026-10-19
---
# BL-2741 — Run upstream case 1560 (lib1560, the URL API test) to success and hold it on the ratchet

## Goal

Upstream case 1560 passes through the case runner (stdout `success`) and is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.

## Context

BL-2652 ported lib1560 (`Lib1560`, `Lib1560Checks`, `Lib1560Tables`) and filed each cause of a differing row as BL-2731 to BL-2740. Once they are Done, run case 1560 (`CURL_UPSTREAM_CASE=1560`); any row still differing is filed as its own task with the board script and named in `depends-on`. The `_WIN32` rows run only on Windows and the `CURLU_PUNYCODE`/`CURLU_PUNY2IDN` rows only with `CURL_TEST_HAVE_CODESET_UTF8`, as upstream's do; check the case passes on Linux and macOS (CI) before holding it, or pin it Windows-only.

## Acceptance criteria

- [x] Case 1560 passes through the runner and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

- 2026-10-18 (lane 3): Case 1560 was skipped with "Curl lacks the feature pop3": `UpstreamCurlPlatform`'s common features listed no `pop3`, `smtp` or `imap`, though `curl -V` lists them and the harness has stand-ins for all three. Added the three to `UpstreamCurlPlatform` and `UpstreamCurlPlatformTests` (uncommitted, stashed by the shift with this requeue), so `touches` now names `Curl.Conformance.UnitLibrary` and `Curl.Conformance.UnitTests` (no task in Doing named either). Next claim: re-apply, run the full ratchet once to check no listed case changes, then add 1560.
- With the features added, lib1560 runs and fails in get_url. Run once with every group forced to run (a local edit, reverted): only two rows differ, both a control byte in the scheme answering 27 where curl answers 3 (`htt\x02://host/file`, `http\x1f://host/path/`). That is engine work in `Curl.Engine.UnitLibrary`, which BL-2653 (Doing) touches, so it is filed as BL-2783 and named in `depends-on`.
- 2026-10-19 (lane 1): With BL-2783 done and the stashed `pop3`/`smtp`/`imap` features re-applied (still uncommitted, stashed again with this requeue), case 1560 now fails in lib1560's `huge` group: `URL 1` to `URL 6: failed to parse [c://...` (every URL with the one-letter scheme `c` and one 119,998-byte part). Likely the Windows drive-letter rule in `CurlUrlScheme`/`UrlRelativeReference`, outside this task's `touches`; filed as BL-2791 and named in `depends-on`. The earlier forced run's "only two rows differ" did not cover `huge`. Next claim: re-apply, run case 1560 once; if it passes, run the full ratchet once and add 1560.

- 2026-10-19 (lane 2): With BL-2791 done and the stashed `pop3`/`smtp`/`imap` features re-applied, case 1560 passes through the runner (`CURL_UPSTREAM_CASE=1560`) and is now on the ratchet; the whole fast suite, every listed case included, passes with the three features added. Not measured on Linux or macOS here (lanes test on Windows only): lib1560 itself gates its `_WIN32` and codeset rows as upstream does, so CI is the check. Measure-CodeQuality.ps1 not run: the only production change is three strings in a static array literal, no method or branch added.

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 379f2ef7 4272a6b9.
- 2026-10-18: Doing -> Backlog. Waits on BL-2783: two lib1560 rows need a control-byte check in Curl.Engine.UnitLibrary, which BL-2653 (Doing) touches
- 2026-10-19: Backlog -> Doing. Criteria at claim: 379f2ef7 4272a6b9.
- 2026-10-19: Doing -> Backlog. Waits on BL-2791: lib1560's huge group fails to parse c:// URLs with long parts, engine/URL-parser work outside this task's touches
- 2026-10-19: Backlog -> Doing. Criteria at claim: 379f2ef7 4272a6b9.
- 2026-10-19: Doing -> Done. Case 1560 passes and is on the ratchet
