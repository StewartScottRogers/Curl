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
- [ ] Cases 1705 and 1706 end `Passed` with `$env:CURL_UPSTREAM_CASE='<n>'` on Windows and are on `PassingUpstreamCases.txt`, or a Notes entry names the part that cannot be matched and a new ADR records it.
- [ ] `curl --ai-help` is unchanged unless an option changes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- 2026-10-21 (interactive replan): where it lands, so it can run beside the other Conformance tasks. The generator goes in a new folder `Curl.Cli.UnitLibrary/ManualPages/` (one type per file), its tests in `Curl.Cli.UnitTests/ManualPages/`. The `managen` interpretation is `Curl.Conformance.UnitLibrary/UpstreamManagenScript.cs`, tested in `Curl.Conformance.UnitTests/UpstreamManagenScriptTests.cs`, and registers itself from its own `Curl.Conformance.UnitTests/UpstreamPerlCommandScripts.Managen.cs` through a module initializer, as `UpstreamPerlCommandScripts.Test1222.cs` does (BL-2994); `UpstreamCaseScreening` admits any registered script, so it needs no change. If `Curl.Conformance.UnitLibrary` lacks a project reference to `Curl.Cli.UnitLibrary`, add it (`Curl.Conformance.UnitLibrary/Curl.Conformance.UnitLibrary.csproj` is in `touches`). If a new ADR is written, add its file to `touches` in the same commit. BL-3000 ports the `cd2nroff` parts `test1222.pl` reads into `Curl.Conformance.UnitLibrary`; whichever lands second may reuse the other's `cd2nroff` code.

## Log

- 2026-10-19: Created.
- 2026-10-21: touches narrowed from the whole Curl.Cli.UnitLibrary, Curl.Conformance.UnitLibrary and Curl.Conformance.UnitTests to the new ManualPages folders, the managen script, its test and its registration file (see Notes), so it no longer overlaps BL-2851, BL-2968, BL-3000 and BL-2834.
- 2026-10-21: Backlog -> Doing. Criteria at claim: e77fbcd3 1c00a759 cd5a64f3 c5f8cef6.
