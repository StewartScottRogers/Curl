---
id: BL-2866
title: Interpret upstream test1707.pl in C# against curl -h <option> and put cases 1707, 1708 and 1710 on the ratchet
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2863, BL-2864]
touches: [Curl.Conformance.UnitLibrary/UpstreamTest1707Script.cs, Curl.Conformance.UnitTests/UpstreamTest1707ScriptTests.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-19
completed: 2026-10-21
---
# BL-2866 — Interpret upstream test1707.pl in C# against curl -h <option> and put cases 1707, 1708 and 1710 on the ratchet

## Goal

Upstream cases 1707, 1708 and 1710 ("Verify curl -h --insecure / -h -F / -h --no-clobber") end `Passed` with `$env:CURL_UPSTREAM_CASE` on Windows, through a C# interpretation of test1707.pl that runs the in-process `curl -h <option>` and compares it with the option's text in the vendored `curl.txt`, and are on `PassingUpstreamCases.txt`.

## Context

Split from BL-2850. Needs the dispatch seam with `%CURL` bound (BL-2863) and the vendored docs (BL-2864, which records whether `curl.txt` is vendored or must be built from the `.md` files). Read test1707.pl at tag `curl-8_22_0`. Where Curl's `-h <option>` text differs from real curl 8.22.0's, file a task to fix `CurlHelpTable` in `Curl.Cli.UnitLibrary` (focused unit test in `Curl.Cli.UnitTests`, ADR-0482, `--ai-help` kept right) rather than widening this task. Never run Perl.

## Acceptance criteria

- [x] Cases 1707, 1708 and 1710 each end `Passed` through the interpretation (`UpstreamTest1707ScriptTests.UpstreamCase_RunThroughTheInterpretation_Passes`); registering it in the ratchet and listing them on `PassingUpstreamCases.txt` moved to BL-2887 (see Notes).
- [x] The interpretation has its own unit tests (a matching and a differing help text).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Curl.Conformance.UnitLibrary` keeps 100% line and branch coverage and complexity at most 10.

## Notes

- curl.txt: BL-2864 found it is not in the tarball. Real curl 8.22.0's `curl -M` (Git's ucrt64 build) is byte-identical to Curl's `CurlManual.txt` and is the text managen generated for that release, so it is vendored as `UpstreamTestData/docs/cmdline-opts/curl.txt` (ADR-0719) rather than waiting for BL-2881's managen. The csproj already copies `docs\cmdline-opts\*`.
- Measured: Curl's `-h --insecure`, `-h -F` and `-h --no-clobber` match real curl 8.22.0 byte for byte (`-h all` differs, but not on those options' lines), so no CurlHelpTable task was needed.
- Split: registering the interpretation needs `UpstreamConformanceTests.cs`, which BL-2841 (Doing) touches, and `UpstreamTestData/README.md`, which BL-2829 (Doing) touches. Listing the cases before registration would fail the ratchet (a listed case that skips), so both moved to BL-2887. The cases are proven to pass by running them through `UpstreamCaseRunner` in this task's tests.
- `%CURL` is ignored as a path: the interpretation calls the runner's in-process curl. The manual's path resolves against `tests` under the source-tree folder, so `../docs/...` reaches `UpstreamTestData/docs`.
- Quality: Curl.Conformance.UnitLibrary measured once; `SplitLines`' unterminated last line was uncovered and is now covered by the differing-text test.

## Log

- 2026-10-19: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 73f7d9a9 c5dc9aeb 309149d6.
- 2026-10-21: Doing -> Backlog. Criteria changed: ratchet registration split to BL-2887 (UpstreamConformanceTests.cs held by BL-2841)
- 2026-10-21: Backlog -> Doing. Criteria at claim: 3df242fa c5dc9aeb 309149d6.
- 2026-10-21: Doing -> Done. UpstreamTest1707Script interprets test1707.pl; cases 1707, 1708, 1710 pass through it; ratchet registration in BL-2887
