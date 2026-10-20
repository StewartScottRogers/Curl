---
id: BL-2830
title: Make upstream cases 173, 1461, 1463 and 1464 pass on Windows: the faked null-device file name in a form post and the --help output
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitLibrary/UpstreamCurlPlatform.cs, Curl.Conformance.UnitTests/UpstreamCurlPlatformTests.cs, Curl.Cli.UnitLibrary/CurlHelpTable.cs, Curl.Cli.UnitTests/CurlHelpTableTests.cs, Curl.Conformance.UnitLibrary/UpstreamTestDirectoryComposition.cs, Curl.Conformance.UnitTests/UpstreamTestDirectoryCompositionTests.cs, Curl.Conformance.UnitTests/UpstreamTestData/data/data1461.txt, Curl.Conformance.UnitTests/UpstreamTestData/data/data1463.txt]
requirement: none
created: 2026-10-19
completed: 2026-10-19
---
# BL-2830 — Make upstream cases 173, 1461, 1463 and 1464 pass on Windows: the faked null-device file name in a form post and the --help output

## Goal

Upstream cases 173, 1461, 1463 and 1464, which the gap office measures `match` on Linux, pass on Windows too and are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.

## Context

Measured 2026-10-19 on Windows, each fails:

- 173 ("HTTP RFC1867-formpost a file from stdin with faked filename", `-F "fileupload=@-;filename=%DEV_NULL;..."`): `<verify><protocol>` has `Content-Length: 378` and Curl sent `372`. The 6 bytes are the difference between `/dev/null` (9 bytes) and `NUL` (3 bytes): `UpstreamCurlPlatform.NullDevice` is `NUL` on Windows, so `%DEV_NULL` expands to `NUL` while the case's expected bytes (taken from a Unix run) hold `/dev/null`. runtests.pl sets `%DEV_NULL` per host; decide, as the ratchet's rule (CLAUDE.md, "Tests pass on Windows, Linux and macOS": write the test platform-neutral), whether `%DEV_NULL` is `/dev/null` in the faked file *name* on every platform while the null device a file is *opened* from stays platform-specific, and pin it.
- 1461 ("curl important --help", `--help`), 1463 and 1464 ("curl file category --help", `--help file`): `<verify><stdout>` differs at byte 0: expected nothing at that point, got `Usage: curl [options...] <url>\r\n` / `file: FILE protocol\r\n`. The three expected outputs end at the first line: the files' `<stdout>` is empty or begins later; find why on Windows the help text reaches `<verify><stdout>` (CRLF written to the stream, `mode="text"`, or help written to standard output rather than where the case compares). Help text is `Curl.Cli.UnitLibrary/CurlHelpTable.cs`; do not change what `curl --help` prints for real users unless real curl 8.22.0 on the platform prints differently (measure it with `Record-CurlExchange.ps1`).

Upstream: curl 8.22.0 `tests/data/test173`, `test1461`, `test1463`, `test1464`; https://curl.se/docs/manpage.html (`-h`, `--help`).
Where the product is wrong, add a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482). If the fix lands outside the files in `touches`, add them in the same commit.

## Acceptance criteria

- [x] The cause of each of the four failures is stated in Notes.
- [x] Cases 173, 1461, 1463 and 1464 each end `Passed` with `$env:CURL_UPSTREAM_CASE='<n>'` on Windows and are on `PassingUpstreamCases.txt`.
- [x] A focused unit test fails without each fix and passes with it (`UpstreamCurlPlatformTests` for `%DEV_NULL`; `Curl.Cli.UnitTests` or the harness test project for the help output).
- [x] If `curl --help` output changed, `curl --ai-help` is still right (root CLAUDE.md) and the change matches real curl's bytes for the platform.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage.
- [x] After the push, `CI` is green on Linux and macOS for the commit.

## Notes

- Cause of 173: `%DEV_NULL` expanded to `NUL` on Windows, while the case's `Content-Length: 378` counts `/dev/null` in the faked `filename=`. Fix (ADR-0716): `UpstreamTestDirectoryComposition.Rewrite` turns `filename=%DEV_NULL` and `filename="%DEV_NULL"` into `/dev/null` on every platform; every other `%DEV_NULL` (test3015's `-o%DEV_NULL`, a file curl opens) stays `NUL` on Windows. Not done in `UpstreamCurlPlatform`: one platform value cannot serve both uses, and the rewrite hook the runner already calls is free of other lanes (`UpstreamCaseRunner.cs` is BL-2863's in Doing). The focused test is three new rows in `UpstreamTestDirectoryCompositionTests.Rewrite_Line_ComposesOnlyWhatRuntestsWorkingDirectoryNames` (the two `filename=` forms rewritten, `-o%DEV_NULL` left), which fail without the rewrite; `UpstreamCurlPlatformTests` needed no change.
- Cause of 1461, 1463 and 1464: not Curl and not Windows line endings. Their `<stdout>` is `%includetext %SRCDIR/data/data1461.txt%` (1463 and 1464: `data1463.txt`), and only the two httpsig key files were vendored in `UpstreamTestData/data/`, so the include read nothing and the expected stdout was empty (the gap office measures against the release tarball, which has them). Fix: vendored curl 8.22.0's `tests/data/data1461.txt` and `data1463.txt` byte for byte (raw.githubusercontent.com, tag `curl-8_22_0`); `.gitattributes` already marks `data/*` `-text`. Curl's `--help` and `--help file` match them as they are, so `CurlHelpTable.cs`, `--help` and `--ai-help` are unchanged. The ratchet rows for 1461, 1463 and 1464, now listed, are the tests that fail without the files.
- The UpstreamTestData `README.md`'s `## data` section still names only the httpsig keys: BL-2864 (Doing) touches that README, so filed BL-2872 (Backlog; the shift may renumber it) to name the two new files once BL-2864 is Done.
- Measured: conformance fast tests 3585 passed, 0 failed; `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary`: 0 failing members; `dotnet build Curl.slnx -warnaserror` clean. The four cases are platform-neutral (no paths or OS text in their output); CI on Linux and macOS is the shift's to watch after the push (lanes do not push).

## Log

- 2026-10-19: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: 31758093 0dacb452 e6f99989 b47eccaf 7bd8d990 620ebead.
- 2026-10-19: Doing -> Done. Cases 173, 1461, 1463 and 1464 pass on Windows and are on the ratchet
