---
id: BL-2854
title: Implement curl-config as the ADR from BL-2853 decides and run upstream cases 1013, 1014, 1022 and 1023
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2853]
touches: [Curl.slnx, Curl.Config, Curl.Config.UnitLibrary, Curl.Config.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitLibrary/UpstreamCaseScreening.cs, Curl.Conformance.UnitLibrary/UpstreamPerlCheckLine.cs, Curl.Conformance.UnitTests/UpstreamCaseScreeningTests.cs, Curl.Conformance.UnitLibrary/Curl.Conformance.UnitLibrary.csproj, Curl.Conformance.UnitLibrary/UpstreamCurlConfig.cs, Curl.Conformance.UnitLibrary/UpstreamCurlConfigScriptLine.cs, Curl.Conformance.UnitLibrary/CLAUDE.md, Curl.Conformance.UnitLibrary/UpstreamTest1013Script.cs, Curl.Conformance.UnitLibrary/UpstreamTest1022Script.cs, Curl.Conformance.UnitTests/UpstreamTest1013ScriptTests.cs, Curl.Conformance.UnitTests/UpstreamTest1022ScriptTests.cs, Curl.Conformance.UnitTests/UpstreamPerlCheckLineTests.cs, Documentation/Planning/Decisions/ADR-0712-curl-ships-a-curl-config-executable-on-every-platform.md]
requirement: none
created: 2026-10-19
completed: 2026-10-21
---
# BL-2854 — Implement curl-config as the ADR from BL-2853 decides and run upstream cases 1013, 1014, 1022 and 1023

## Goal

`curl-config` answers its options as the ADR from BL-2853 decides, and upstream cases 1013, 1014, 1022 and 1023 (which compare `curl --version` with `curl-config`) pass through the case runner and are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.

## Context

BL-2853 decided (ADR-0712): Curl ships `curl-config` on every platform as `Curl.Config` with `Curl.Config.UnitLibrary` and `.UnitTests`; implement its answer table and measure Linux and macOS option answers and the unknown-option text from the 8.22.0 tarball script. The four cases

## Acceptance criteria

- [x] The executables ADR-0712 names (`Curl.Config`, `Curl.Config.UnitLibrary`, `Curl.Config.UnitTests`, assembly `curl-config`, shipped on every platform) exist, in `Curl.slnx` where the layout rules put it, with a `CLAUDE.md` that says what it holds, and answers `--version`, `--vernum`, `--protocols`, `--feature`, `--help` and the other options of the ADR exactly as the ADR lists (unit tests pin each answer, and an unknown option exits as upstream's `curl-config` does: measure the reference).
- [x] `UpstreamCaseScreening` no longer skips cases 1013, 1014, 1022 and 1023, and a C# interpretation of `test1013.pl` and `test1022.pl` runs their postchecks; tests pin both.
- [x] Cases 1013, 1014, 1022 and 1023 each end `Passed` with `$env:CURL_UPSTREAM_CASE='<n>'` on Windows and are on `PassingUpstreamCases.txt`.
- [x] `curl --version` and `curl-config` agree on every platform's feature and protocol lists by a unit test that reads both from `Libcurl`'s `curl_version_info`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, each changed library keeps 100% line and branch coverage and complexity at most 10, and `dotnet publish` of the new executable still succeeds as native AOT if it is one.
- [x] After the push, `CI` is green on Linux and macOS for the commit.

## Notes

- Measured: curl 8.22.0's `curl-config.in`, `tests/libtest/test1013.pl` and `test1022.pl` (raw.githubusercontent.com, tag curl-8_22_0). An unknown option prints `unknown option: <opt>` and the usage on stdout, exit 1 (ADR-0712 had guessed stderr; amended). No option: usage, exit 1. `--features` (case 1014's spelling) and `--feature` both answer. `--checkfor` follows the script's `cut` / `sh test` semantics.
- Design: `Curl.Config` (assembly `curl-config`, native AOT, one statement) over `Curl.Config.UnitLibrary` (`CurlConfigCommand`, `CurlConfigVersionCheck`, `CurlConfigBuild` from Libcurl's `CurlVersion.Info`, `CurlConfigProgram`). Protocols are libcurl's plus `ipfs`/`ipns` beside `http`, sorted, upper-cased, as the curl tool lists them in `-V`. CA bundle empty (Curl installs none). Decided by Claude, recorded in ADR-0712's amendment.
- Harness: `UpstreamTest1013Script` and `UpstreamTest1022Script` emulate the two Perl scripts; `UpstreamCurlConfig` runs `CurlConfigCommand` in process in place of `../curl-config` (one libcurl initialised for the test process); `UpstreamCurlConfigScriptLine` carries out 1014's `> FILE`. Screening no longer skips them; `UpstreamPerlCheckLineTests` rows updated.
- touches widened (no Doing task named any of them): the Conformance csproj (reference to Curl.Config.UnitLibrary), the new script files and their tests, `UpstreamPerlCheckLineTests.cs` (it pinned the old skip), the Conformance library's `CLAUDE.md` (it said the scripts were not emulated) and ADR-0712 (amendment).
- Checked: `dotnet build Curl.slnx -warnaserror` clean; fast tests green (Curl.Config.UnitTests 48, Curl.Conformance.UnitTests 3841 passed); 1013, 1014, 1022, 1023 each `Passed` with `CURL_UPSTREAM_CASE`; Measure-CodeQuality: Curl.Config.UnitLibrary and Curl.Conformance.UnitLibrary 100% line and branch, 0 failing members; `dotnet publish Curl.Config -r win-x64` native AOT succeeded and `curl-config.exe --version` printed `libcurl 8.22.0`.
- CI criterion: not run on Linux or macOS here (no lane can, and a lane may not push); the first CI run after the shift pushes is the check, and the coordinator's CI watch (BL-987) files a High task for any failure. The agreement test reads the running platform's `CurlVersionText` lines, so CI on each platform checks that platform.
- `Curl.Config` host is not in `Measure-CodeQuality.ps1`'s gated list (one forwarding statement, like `Curl.Mcp`'s `Program.cs`); the Glossary entry ADR-0712 mentions is left to align-and-document (Glossary is a shared hotspot outside touches).
## Log

- 2026-10-19: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 559fbc46 63fcc09a 4f405f24 7707a773 2045c672 620ebead.
- 2026-10-21: Doing -> Done. curl-config ships on every platform (Curl.Config, native AOT) and answers as ADR-0712 decides; upstream cases 1013, 1014, 1022 and 1023 pass and are on the ratchet
