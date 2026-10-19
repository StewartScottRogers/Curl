---
id: BL-2830
title: Make upstream cases 173, 1461, 1463 and 1464 pass on Windows: the faked null-device file name in a form post and the --help output
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitLibrary/UpstreamCurlPlatform.cs, Curl.Conformance.UnitTests/UpstreamCurlPlatformTests.cs, Curl.Cli.UnitLibrary/CurlHelpTable.cs, Curl.Cli.UnitTests/CurlHelpTableTests.cs]
requirement: none
created: 2026-10-19
completed:
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

- [ ] The cause of each of the four failures is stated in Notes.
- [ ] Cases 173, 1461, 1463 and 1464 each end `Passed` with `$env:CURL_UPSTREAM_CASE='<n>'` on Windows and are on `PassingUpstreamCases.txt`.
- [ ] A focused unit test fails without each fix and passes with it (`UpstreamCurlPlatformTests` for `%DEV_NULL`; `Curl.Cli.UnitTests` or the harness test project for the help output).
- [ ] If `curl --help` output changed, `curl --ai-help` is still right (root CLAUDE.md) and the change matches real curl's bytes for the platform.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage.
- [ ] After the push, `CI` is green on Linux and macOS for the commit.

## Notes

## Log

- 2026-10-19: Created.
