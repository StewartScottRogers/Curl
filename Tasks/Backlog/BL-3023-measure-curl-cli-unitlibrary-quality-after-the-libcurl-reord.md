---
id: BL-3023
title: Measure Curl.Cli.UnitLibrary quality after the --libcurl reorder and confirm CI is green on Linux and macOS
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3022]
touches: [Curl.Cli.UnitLibrary/LibcurlSourceCode.cs, Curl.Cli.UnitLibrary/LibcurlSourceCode.ProtocolOptions.cs, Curl.Cli.UnitTests/LibcurlSourceCodeOptionCoverageTests.cs, Curl.Cli.UnitTests/LibcurlSourceCodeTests.cs]
requirement: none
created: 2026-10-21
completed:
---
# BL-3023 — Measure Curl.Cli.UnitLibrary quality after the --libcurl reorder and confirm CI is green on Linux and macOS

## Goal

`Curl.Cli.UnitLibrary` keeps 100% line and branch coverage, complexity at most 10 and CRAP at most 30 after BL-3022's reorder, and CI is green on Linux and macOS for BL-3022's commit.

## Context

Split from BL-2966. Run `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` once (the script's header gives the options). The reordered methods are in `LibcurlSourceCode.cs` and `LibcurlSourceCode.ProtocolOptions.cs`; close any gap with focused tests in `LibcurlSourceCodeOptionCoverageTests.cs` or `LibcurlSourceCodeTests.cs` (add a file to `touches` in the same commit if a gap lies elsewhere). Never change `CodeMetricsConfig.txt`. After BL-3022's commit is pushed, read the `CI` run for it with `gh run list --workflow CI --branch work/dark-factory` and `gh run view <id> --json jobs`: the Windows, Linux and macOS jobs must pass. If a job fails on Linux or macOS, the coordinator files a "Fix CI failure" task; add `depends-on` to this task for it or note it, do not fix it here.

## Acceptance criteria

- [ ] Measure-CodeQuality for `Curl.Cli.UnitLibrary` reports no member of `LibcurlSourceCode*` outside the four gates.
- [ ] Notes record the CI run id and that its Windows, Linux and macOS jobs and the quality job passed for a commit that contains BL-3022's work.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes for `Curl.Cli.UnitTests`.

## Notes

## Log

- 2026-10-21: Created.
