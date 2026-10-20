---
id: BL-2855
title: Make the SFTP directory cases 613, 614 and 1446 pass: the test613.pl prepared tree, the listing text and the remote time
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitLibrary/UpstreamTest613Script.cs, Curl.Conformance.UnitLibrary/UpstreamSshServer.cs, Curl.Conformance.UnitTests/UpstreamSshServerTests.cs, Curl.Conformance.UnitTests/UpstreamTest613ScriptTests.cs, Curl.Conformance.SshServer.UnitLibrary, Curl.Conformance.SshServer.UnitTests]
requirement: none
created: 2026-10-19
completed:
---
# BL-2855 — Make the SFTP directory cases 613, 614 and 1446 pass: the test613.pl prepared tree, the listing text and the remote time

## Goal

Upstream cases 613 (SFTP directory retrieval), 614 (SFTP pre-quote chmod) and 1446 (SFTP with `--remote-time`) pass through the case runner and the SSH stand-in on Windows and are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, or have a recorded reason.

## Context

Gap scorecard 2026-10-19_1329 has the three `unmeasured | harness-unsupported` on Linux (the gap tool's `/usr/bin/perl` path defeats the precheck, BL-2857). Measured 2026-10-19 on Windows they run and fail:

- 613 and 614: `the --output file against <reply><data> differs at byte 1 (line 1)`: the case expects the canonical listing line `d?????????    N U         U               N ???  N NN:NN asubdir` and Curl's output holds `drwxr-xr-x   1 curltest curltest        0 Oct 19 23:25 .` - the `.` entry and an unprocessed long listing. In upstream, `%PERL %SRCDIR/libtest/test613.pl postprocess <dir> <log>` rewrites the stored listing into the canonical form and drops `.` and `..`; `UpstreamTest613Script` emulates that (its class comment describes `prepare` and `postprocess`). Find why the postprocess does not reach the file the case compares: the order of the postcheck and the comparison, which file `LOG` names, or the SFTP stand-in (`UpstreamSshServer`, `Curl.Conformance.SshServer.UnitLibrary`) listing `.` and `..` itself.
- 1446: `postcheck FAILED: exit code 1`: `test613.pl postprocess <dir> <log> <MTIME>` exits 1 unless the downloaded file was last written at the MTIME the SFTP server reported; check that the stand-in's SFTP attribute reply carries the file's modification time and that `--remote-time` applies it (`Curl.Protocol.Ssh.UnitLibrary`).

Upstream: curl 8.22.0 `tests/data/test613`, `test614`, `test1446` and `tests/libtest/test613.pl` (https://github.com/curl/curl/tree/curl-8_22_0/tests). Where Curl itself is wrong (not the stand-in), fix it with a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482) and add the folder to `touches` in the same commit.

## Acceptance criteria

- [ ] The cause of each failure is stated in Notes.
- [ ] Cases 613, 614 and 1446 each end `Passed` with `$env:CURL_UPSTREAM_CASE='<n>'` on Windows and are on `PassingUpstreamCases.txt`; any that cannot pass has its reason in Notes.
- [ ] A focused unit test fails without each fix: `UpstreamTest613ScriptTests` or `UpstreamSshServerTests` for the harness, the library's `.UnitTests` for a product fix.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10.
- [ ] After the push, `CI` is green on Linux and macOS for the commit.

## Notes

## Log

- 2026-10-19: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: b18c1ec7 0f5f6907 c061550a dd1cc079 620ebead.
