---
id: BL-2224
title: Give the upstream case runner values for %PWD, %LIBTESTS and %SSHKEYALGO so cases 726, 1145, 3009, 518, 537, 678, 1960 and 1459 run
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-14
completed:
---
# BL-2224 — Give the upstream case runner values for %PWD, %LIBTESTS and %SSHKEYALGO so cases 726, 1145, 3009, 518, 537, 678, 1960 and 1459 run

## Goal

Give the upstream case runner values for `%PWD`, `%LIBTESTS` and `%SSHKEYALGO`, so cases 726, 1145, 3009, 518, 537, 678, 1960 and 1459 stop skipping "the harness has no value for ...".

## Context

Source: Stewart's 2026-10-14 request to raise the gap office's behaviour score (aim 97-99%) by making the shared in-process harness run cases it leaves unmeasured; the 8.22.0 gap run 2026-10-13_1406 had 2 `unknown-variable` and 38 `harness-unsupported` cases, and running `Curl.Conformance.UnitTests` on 2026-10-14 names these eight. The gap tool uses `Curl.Conformance.UnitLibrary` (it substitutes `%PWD` and `%SRCDIR` in the bytes itself before the runner reads them; the unit tests do not, so the runner should own the values).

- `%PWD` (726 sets `HOME=%PWD`; 1145 `file://bad-host%PWD/%LOGDIR/...`; 3009 `--output-dir %PWD/not-there`): `RunAsync` has a value only when the caller names a tests directory (`UpstreamCaseRunner`); make the runner give it the same value `runtests.pl` does (the tests folder, forward slashes, no blank in it) when the caller names a source/tests directory, and have `UpstreamConformanceTests` pass `UpstreamTestData`.
- `%LIBTESTS` (537, 518, 678 https, 1960): `runtests.pl` sets it to the libtest runner path; read these four cases first - if each also needs a `lib` program they are libcurl-API cases already excluded by the gap converter, in which case `%LIBTESTS` should resolve to a path so the case can report the clearer "the harness does not act on <client><tool>" reason (say so in Notes and do not invent a runner).
- `%SSHKEYALGO` (1459, sftp): the host key algorithm name the SSH stand-in's host key advertises in a `known_hosts` line (`ssh-rsa` for the RSA host key used on Windows, `ssh-ed25519` elsewhere); `UpstreamSshServer` already carries the fingerprints, so add the algorithm name beside them.

## Acceptance criteria

- [ ] `%PWD`, `%LIBTESTS` (as decided above) and `%SSHKEYALGO` have values in `UpstreamCaseRunner`, each pinned by a test in `UpstreamCaseRunnerTests` / `UpstreamSshServerTests`.
- [ ] Cases 726, 1145, 3009 and 1459 are no longer screened for a variable; 518, 537, 678 and 1960 are classified in Notes with the true reason they still skip, if they do. Each that passes is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; each that runs but fails is named in Notes as a real gap, with a follow-up task per distinct cause.
- [ ] Platform-neutral tests; `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary` shows no member outside a gate; `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` is green; `Curl.Conformance.UnitLibrary/CLAUDE.md` lists the new variables.

## Notes

## Log

- 2026-10-14: Created.
- 2026-10-14: Backlog -> Doing.
