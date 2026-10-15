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
completed:
---
# BL-2240 — List sftp and scp in UpstreamCurlPlatform's features so SSH cases needing them, such as 1459, run

## Goal

`UpstreamCurlPlatform` lists `sftp` and `scp` (the protocols Curl serves and the harness's SSH stand-in emulates), so SSH cases whose `<features>` name them, such as test1459, run instead of skipping "Curl lacks the feature sftp".

## Context

Found by BL-2224 (2026-10-14): once `%SSHKEYALGO` has a value, test1459 (SFTP with corrupted known_hosts, expects exit 60) skips for "Curl lacks the feature sftp", because `UpstreamCurlPlatform.CommonFeatures` (`Curl.Conformance.UnitLibrary/UpstreamCurlPlatform.cs`) lists no `sftp` or `scp`. SFTP and SCP cases without a `<features>` part (600 to 665) already run through `UpstreamSshServer`. 59 vendored cases name `sftp` or `scp` on a line of their own; adding the features opens the ones that list them in `<features>`, and each must be judged.

## Acceptance criteria

- [ ] `UpstreamCurlPlatform` lists `sftp` and `scp`, pinned by a test in `UpstreamCurlPlatformTests`, and its remarks say so.
- [ ] Every case the change opens either passes and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, or is named in Notes with its first difference and a follow-up task per distinct cause; test1459's outcome is recorded.
- [ ] `dotnet build -warnaserror` clean, `dotnet test --filter "TestCategory!=Integration"` green, `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary` shows no member outside a gate.

## Notes

## Log

- 2026-10-14: Created.
- 2026-10-14: Backlog -> Doing.
