---
id: BL-2328
title: Close GF-0085: The stderr of a -OJ --output-dir download whose directory is a regular file differs from curl's on Linux (on linux)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Protocol.Abstractions.UnitLibrary]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2328 — Close GF-0085: The stderr of a -OJ --output-dir download whose directory is a regular file differs from curl's on Linux (on linux)

## Goal

Curl behaves as curl 8.22.0 does for every item of gap finding GF-0085 (The stderr of a -OJ --output-dir download whose directory is a regular file differs from curl's on Linux), so a later gap analysis measures each of `behaviour:test3036` as `match`.

## Context

- Finding: GF-0085, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: Medium. Introduced in: not stated upstream.
- Platform: linux, measured against curl 8.22.0 built from the release tarball with OpenSSL by the gap-platforms workflow (.github/workflows/gap-platforms.yml, ADR-0505). This gap is linux's own: Windows does not show it.
- Lanes test only on Windows, so a lane cannot see this gap pass locally. The proof is the gap-platforms workflow (run it on the branch: gh workflow run gap-platforms.yml -f ref=<branch>) or the CI job on linux passing a test that pins each item; write those tests platform-neutral or mark them for linux.
- Items: `behaviour:test3036`.
- Targeted curl version: 8.22.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.22.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

behaviour:test3036 (http://127.0.0.1:<port>/ --no-clobber --output-dir %LOGDIR/present -OJ --retry 1 --retry-all-errors, where %LOGDIR/present is a file): expected 'reference curl exits 23; stdout 0 bytes', actual 'stderr differs'. The exit code and request bytes agree (the cross-check compares them first). Same on macOS. Matches on Windows. Reproduce on linux: pwsh -File Gap/Tools/Measure-ReferenceCrossCheck.ps1 -Measurement <run>/measurements/linux/behaviour.json -TestsData <release>/tests/data -OutFile $TMPDIR/behaviour.json

Suggestion, copied from the finding:

Make Curl.Console's 'Warning: Failed to open the file <path>: <reason>' line (OutputFileOpenWarning.ReasonFor) give the platform's strerror text. Today it hard-codes the Windows C runtime's texts, measured with Schannel curl. Opening <file>/MMM3036MMM fails on Linux and macOS with ENOTDIR, and curl prints 'Not a directory', while Windows gives 'No such file or directory'. Map the failure to Unix's text off Windows. Curl.Engine.UnitLibrary CRuntimeErrorNumbers and Curl.Cli.UnitLibrary DiskDataFileReader already hold 'Not a directory'. This needs a FileAccessStatus value (Curl.Protocol.Abstractions.UnitLibrary) for a non-directory path component, so check any other Unix-only wording in the retry and warning lines too. This is the most likely cause; the cross-check does not store the two binaries' stderr. Pin each platform's text in its own test in Curl.Console.UnitTests, the Unix one under [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)].

## Acceptance criteria

- [x] `behaviour:test3036`: Curl answers what curl 8.22.0 answers on linux, `reference curl exits 23; stdout 0 bytes: `, so the item measures `match`.
- [x] The gap-platforms workflow run on the branch (or the CI job on linux) passes for the named items; lanes test only on Windows, so a green Windows run alone does not close this task.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- Cause: off Windows .NET raises `ENOTDIR` (opening `present/MMM3036MMM` with `present` a
  file) as a plain `IOException` whose `HResult` is the raw errno 20, which `FileOpenFailure`
  mapped to `IoError`, so the warning said `Invalid argument` where curl says `Not a directory`.
  Windows reports `ERROR_PATH_NOT_FOUND` (`DirectoryNotFoundException`, `No such file or
  directory`), which already matched.
- Fix: new `FileAccessStatus.NotADirectory` (Abstractions); `FileOpenFailure.IsNotADirectory`
  maps errno 20 (Linux and macOS alike) to it (Core, `StatusForErrorCode` split out to keep
  `StatusFor` under the complexity gate); `OutputFileOpenWarning.ReasonFor` gives `Not a directory`.
  Every other reader of `FileAccessStatus` falls into its default branch for the new value, as
  it did for `IoError`, so no other wording changes.
- Touches: added `Curl.Core.UnitLibrary` and `Curl.Core.UnitTests` (the exception mapping lives
  there); no other task was in Doing on `origin/work/dark-factory`.
- Tests: `PhysicalFileSystemTests.OpenForWriteAsync_ParentIsAFileOnPosix_IsNotADirectory`
  (Linux/macOS/FreeBSD only) and `..._ParentIsAFileOnWindows_IsNotFound` pin each platform on
  the real disk in a temporary directory; `FileOpenFailureTests` pins the errno mapping;
  `OutputFileOpenWarningTests` pins test3036's line. Lanes test on Windows only: the Linux
  proof is CI's Linux job running the POSIX pin once the shift pushes (the coordinator files a
  task if it is red), and GF-0085 closes only when a later gap run measures test3036 `match`.
- Quality: Measure-CodeQuality for Curl.Console, Curl.Core.UnitLibrary and
  Curl.Protocol.Abstractions.UnitLibrary at 100% line and branch, 0 failing members.
- No option changed, so `--ai-help` needs no change.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. Off Windows an -o open whose parent is a file now warns 'Not a directory' (ENOTDIR), as curl does in upstream test 3036
