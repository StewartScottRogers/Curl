---
id: BL-2240
title: List sftp and scp in UpstreamCurlPlatform's features so SSH cases needing them, such as 1459, run
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-14
completed: 2026-10-14
---
# BL-2240 — List sftp and scp in UpstreamCurlPlatform's features so SSH cases needing them, such as 1459, run

## Goal

`UpstreamCurlPlatform` lists `sftp` and `scp` (the protocols Curl serves and the harness's SSH stand-in emulates), so SSH cases whose `<features>` name them, such as test1459, run instead of skipping "Curl lacks the feature sftp".

## Context

Found by BL-2224 (2026-10-14): once `%SSHKEYALGO` has a value, test1459 (SFTP with corrupted known_hosts, expects exit 60) skips for "Curl lacks the feature sftp", because `UpstreamCurlPlatform.CommonFeatures` (`Curl.Conformance.UnitLibrary/UpstreamCurlPlatform.cs`) lists no `sftp` or `scp`. SFTP and SCP cases without a `<features>` part (600 to 665) already run through `UpstreamSshServer`. 59 vendored cases name `sftp` or `scp` on a line of their own; adding the features opens the ones that list them in `<features>`, and each must be judged.

## Acceptance criteria

- [x] `UpstreamCurlPlatform` lists `sftp` and `scp`, pinned by a test in `UpstreamCurlPlatformTests`, and its remarks say so.
- [x] Every case the change opens either passes and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, or is named in Notes with its first difference and a follow-up task per distinct cause; test1459's outcome is recorded.
- [x] `dotnet build -warnaserror` clean, `dotnet test --filter "TestCategory!=Integration"` green, `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary` shows no member outside a gate.

## Notes

- Of the vendored cases, five name `sftp` or `scp` in `<features>`: 632, 1459, 2604, 2605 and 445. No case uses them in `%if`. 2604 and 2605 still skip for `unittest`, and 445 for `ftp`, `ldap`, `rtsp` and others, so the change opens 632 and 1459.
- test632 and test1459 both pass (run with the manual one-case test on Windows) and are on `PassingUpstreamCases.txt`. test1459 (SFTP with corrupted known_hosts) gets exit 60 as expected.
- The pinning test is `AllPlatforms_ListTheSshProtocolsTheHarnessEmulates` (sftp, scp on Windows, Linux and macOS); the test class's copy of the common feature list gains both too.
- Fast tests: 37 test projects passed, 0 failed (Curl.Conformance.UnitTests 2931 passed, 760 skipped). Measure-CodeQuality for Curl.Conformance.UnitLibrary: 0 failing members.

## Log

- 2026-10-14: Created.
- 2026-10-14: Backlog -> Doing.
- 2026-10-14: Doing -> Done. UpstreamCurlPlatform lists sftp and scp; SSH cases 632 and 1459 run and pass
