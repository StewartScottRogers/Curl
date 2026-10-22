---
id: BL-3023
title: Measure Curl.Cli.UnitLibrary quality after the --libcurl reorder and confirm CI is green on Linux and macOS
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3022, BL-3021]
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

- [x] Measure-CodeQuality for `Curl.Cli.UnitLibrary` reports no member of `LibcurlSourceCode*` outside the four gates.
- [ ] Notes record the CI run id and that its Windows, Linux and macOS jobs and the quality job passed for a commit that contains BL-3022's work.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes for `Curl.Cli.UnitTests`.

## Notes

- 2026-10-21 (lane 1, first claim): Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary on 271ce9c41: no `LibcurlSourceCode*` member fails a gate. The library's 47 failing members (98.09% line, 95.58% branch) are all in `ManualPages/` from b68112072 (BL-3018's managen port), work that BL-3019, BL-3020 and BL-3021 cover. `dotnet build Curl.slnx -warnaserror` is clean; Curl.Cli.UnitTests passes 4003, 18 skipped, 0 failed.
- CI for BL-3022's commit d6d35bf61 is run 39718635967: Build and test passed on windows-latest, ubuntu-latest and macos-latest, but Quality gates failed on `ConnectionPeer.Create` (Curl.Core) and `Unit2413ConnectionPeer.Mismatch` (Curl.Conformance.UnitProgram). Both were fixed later in 879349362 and 9ad7757f2. Run 39720875902 on 1097ca82b, which has BL-3022's work and both fixes, passed all three build and test jobs. Its Quality gates job failed only on the 47 Curl.Cli ManualPages members; none is in `LibcurlSourceCode*`.
- Requeued with depends-on BL-3021 (Doing on the shared branch; BL-3019 and BL-3020 are already Done). The quality job cannot pass until BL-3021 lands the rest of the ManualPages coverage. Next claim: find the first CI run after BL-3021 is Done, check that its three build and test jobs and its Quality gates job passed, record the run id here, and tick the second criterion. The other two criteria are already verified above.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: f1c8149c 809c786e 48b212cd.
- 2026-10-21: Doing -> Backlog. Waits for BL-3021: CI's quality job is red only on the ManualPages members BL-3021 is covering; LibcurlSourceCode* passes every gate and the build and test jobs pass on all three OSes
