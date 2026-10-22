---
id: BL-2881
title: Build the manual page generator that upstream's managen is, and run cases 1705 and 1706 against it
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2850]
touches: [Curl.Cli.UnitLibrary/ManualPages, Curl.Cli.UnitTests/ManualPages, Curl.Conformance.UnitLibrary/UpstreamManagenScript.cs, Curl.Conformance.UnitLibrary/Curl.Conformance.UnitLibrary.csproj, Curl.Conformance.UnitTests/UpstreamManagenScriptTests.cs, Curl.Conformance.UnitTests/UpstreamPerlCommandScripts.Managen.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-19
completed:
---
# BL-2881 — Build the manual page generator that upstream's managen is, and run cases 1705 and 1706 against it

## Goal

Curl builds a man page and an ASCII page from option documents (Markdown with a header block) the way upstream's `scripts/managen` does, and upstream cases 1705 and 1706 pass against it, each on `PassingUpstreamCases.txt` or with a recorded reason.

## Context

Found by BL-2852 (ADR for cases 1135 to 1706). Upstream cases 1705 "managen makes man page" and 1706 "managen makes ASCII page" run `%SRCDIR/../scripts/managen -d %LOGDIR -I %SRCDIR/../include mainpage option1.md option2.md` (1706 with `ascii`) and compare the generated page with the expected text in the case. Curl's `-M`/`--manual` text is stored, not generated (`CurlManual.txt`, ADR-0069), so no Curl code turns option documents into a page. A complete reimplementation has one: build a generator in `Curl.Cli.UnitLibrary` that reads option documents and renders the nroff page and the ASCII page, hand-written, BCL only. Read upstream's `scripts/managen` and `scripts/cd2nroff` at tag `curl-8_22_0` (https://github.com/curl/curl/tree/curl-8_22_0/scripts) first; `option1.md` and `option2.md` are in the case itself. The `<command type="perl">` mechanism is BL-2850's (a C# interpretation of the named script), which this task extends with `managen`.

## Acceptance criteria

- [ ] A generator in `Curl.Cli.UnitLibrary` renders the nroff page and the ASCII page from option documents, with focused unit tests in `Curl.Cli.UnitTests` at 100% line and branch coverage and complexity at most 10.
- [ ] `managen` is registered as a `<command type="perl">` script (`UpstreamManagenScript`), and its tests render case 1705's and 1706's option documents to their expected pages byte for byte. Running the cases themselves moved to BL-3001 (see Notes).
- [ ] `curl --ai-help` is unchanged unless an option changes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- 2026-10-21 (interactive replan): where it lands, so it can run beside the other Conformance tasks. The generator goes in a new folder `Curl.Cli.UnitLibrary/ManualPages/` (one type per file), its tests in `Curl.Cli.UnitTests/ManualPages/`. The `managen` interpretation is `Curl.Conformance.UnitLibrary/UpstreamManagenScript.cs`, tested in `Curl.Conformance.UnitTests/UpstreamManagenScriptTests.cs`, and registers itself from its own `Curl.Conformance.UnitTests/UpstreamPerlCommandScripts.Managen.cs` through a module initializer, as `UpstreamPerlCommandScripts.Test1222.cs` does (BL-2994); `UpstreamCaseScreening` admits any registered script, so it needs no change. If `Curl.Conformance.UnitLibrary` lacks a project reference to `Curl.Cli.UnitLibrary`, add it (`Curl.Conformance.UnitLibrary/Curl.Conformance.UnitLibrary.csproj` is in `touches`). If a new ADR is written, add its file to `touches` in the same commit. BL-3000 ports the `cd2nroff` parts `test1222.pl` reads into `Curl.Conformance.UnitLibrary`; whichever lands second may reuse the other's `cd2nroff` code.
- 2026-10-21 (lane 1, split before any code): the cases cannot run from this task's `touches`. Their data files (`data1705-*.md`, `data1705-stdout.1`, `data1706-*`) are not vendored, and `%DATE` and `%VERNUM` have no value in `UpstreamCaseRunner`, so both cases skip. Filed BL-3001 (depends on this task) for the vendoring, the two variables and the ratchet lines; this task builds the generator and the script and pins both expected pages in unit tests with the upstream text inline. `managen` needs no `cd2nroff`: the case runs `mainpage` and `ascii` only, so only those commands are ported (`listhelp`, `single`, `protos`, `listcats` print the usage line). The date comes from `SOURCE_DATE_EPOCH` in the invocation's environment (UTC), else the time provider's UTC date, as runtests.pl sets that variable.

- 2026-10-21 (lane 1, requeued at the cost cap): the generator is written and left uncommitted for the next claim (the shift stashes it): `Curl.Cli.UnitLibrary/ManualPages/` (`ManualPageGenerator` public; `ManualPageContext`, `ManualOptionIndex`, `ManualOptionHeader`, `ManualOptionPage`, `ManualMarkdownRenderer`, `ManualAsciiLayout`, `ManualNroffText`, `ManualPerlValues`, `ManualPageExit` internal), `Curl.Conformance.UnitLibrary/UpstreamManagenScript.cs`, its registration `Curl.Conformance.UnitTests/UpstreamPerlCommandScripts.Managen.cs`, and the `Curl.Cli.UnitLibrary` reference in `Curl.Conformance.UnitLibrary.csproj`. `ManualPageGeneratorTests` renders case 1705's man page and 1706's ASCII page byte for byte (upstream inputs and expected pages copied to `Curl.Cli.UnitTests/ManualPages/Upstream/`, read through `[CallerFilePath]`); the case's 1706 `<stdout>` is the file plus one empty line. `dotnet build Curl.slnx -warnaserror` is clean and the fast tests of Curl.Cli.UnitTests (3970) and Curl.Conformance.UnitTests (3998) pass. Left: focused tests for every branch (each refusal and warning in `ManualOptionHeader`, `ManualMarkdownRenderer` and `ManualOptionPage`, `-c`, `CURL_MAKETGZ_VERSION`, `%GLOBALS`, long ASCII examples, quoted lines too wide) to reach 100% line and branch coverage, `UpstreamManagenScriptTests.cs` (both `PageDate` branches, a run over a temporary folder), one `Measure-CodeQuality.ps1 -ReportPath` run per library, then commit. No new task: the work needs this task's stashed code.

## Log

- 2026-10-19: Created.
- 2026-10-21: touches narrowed from the whole Curl.Cli.UnitLibrary, Curl.Conformance.UnitLibrary and Curl.Conformance.UnitTests to the new ManualPages folders, the managen script, its test and its registration file (see Notes), so it no longer overlaps BL-2851, BL-2968, BL-3000 and BL-2834.
- 2026-10-21: Backlog -> Doing. Criteria at claim: e77fbcd3 1c00a759 cd5a64f3 c5f8cef6.
- 2026-10-21: Doing -> Backlog. Cost cap: generator built, both upstream pages render byte for byte, build and fast tests green; coverage tests and the quality run remain (Notes), code left for the next claim.
