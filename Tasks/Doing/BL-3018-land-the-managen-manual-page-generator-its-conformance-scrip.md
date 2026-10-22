---
id: BL-3018
title: Land the managen manual page generator, its Conformance script and UpstreamManagenScript tests (cases 1705 and 1706 pages byte for byte)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/ManualPages, Curl.Cli.UnitTests/ManualPages, Curl.Conformance.UnitLibrary/UpstreamManagenScript.cs, Curl.Conformance.UnitLibrary/Curl.Conformance.UnitLibrary.csproj, Curl.Conformance.UnitTests/UpstreamManagenScriptTests.cs, Curl.Conformance.UnitTests/UpstreamPerlCommandScripts.Managen.cs]
requirement: none
created: 2026-10-21
completed:
---
# BL-3018 — Land the managen manual page generator, its Conformance script and UpstreamManagenScript tests (cases 1705 and 1706 pages byte for byte)

## Goal

The managen manual page generator in `Curl.Cli.UnitLibrary/ManualPages/` and its `<command type="perl">` script `UpstreamManagenScript` are committed, with `UpstreamManagenScriptTests` passing and the byte-for-byte page tests for cases 1705 and 1706 green.

## Context

Split from BL-2881 (its Notes in `Tasks/Deferred/BL-2881-*.md` give the design: upstream's `scripts/managen` at tag `curl-8_22_0`, only `mainpage` and `ascii` ported, the date from `SOURCE_DATE_EPOCH` else the time provider's UTC date, helper types public because `Curl.Cli.UnitTests` has no `InternalsVisibleTo`). The generator is written and building and sits in a stash that the dark factory applies for this task: stash `d04b5e71f8b78a4dbe2c27d14b6679bdc848ce2b` (`ManualPageGenerator`, `ManualPageContext`, `ManualOptionIndex`, `ManualOptionHeader`, `ManualOptionPage`, `ManualMarkdownRenderer`, `ManualAsciiLayout`, `ManualNroffText`, `ManualPerlValues`, `ManualPageExit`; `UpstreamManagenScript.cs`, `UpstreamPerlCommandScripts.Managen.cs`, the `Curl.Cli.UnitLibrary` reference in `Curl.Conformance.UnitLibrary.csproj`; the `ManualPageGeneratorTests` with upstream inputs and expected pages under `Curl.Cli.UnitTests/ManualPages/Upstream/`, and the `ManualNroffText`, `ManualPerlValues` and `ManualOptionIndex` tests). If the shift reports it did not apply, `git diff <sha>^1 <sha>` shows the tracked changes and `git show --stat <sha>^3` the added files.

Left in this task: write `Curl.Conformance.UnitTests/UpstreamManagenScriptTests.cs` (both `PageDate` branches, and a run over a temporary folder that creates and deletes its own files), then build, run the fast tests of `Curl.Cli.UnitTests` and `Curl.Conformance.UnitTests`, and commit. Branch coverage of the generator's other types is BL-3019, BL-3020 and BL-3021, so this task does not need to reach 100% on them. Running cases 1705 and 1706 themselves is BL-3002.

## Acceptance criteria

- [ ] `managen` is registered as a `<command type="perl">` script (`UpstreamManagenScript`) and `UpstreamManagenScriptTests` covers its date rule (`SOURCE_DATE_EPOCH` set and unset) and a run that writes the page into a temporary folder.
- [ ] `ManualPageGeneratorTests` renders case 1705's man page and case 1706's ASCII page byte for byte from the upstream inputs and passes.
- [ ] `curl --ai-help` is unchanged (no option changed).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes for `Curl.Cli.UnitTests` and `Curl.Conformance.UnitTests`.

## Notes

- 2026-10-21: Split from BL-2881. The stash was re-stored under this task's ID (same commit d04b5e71f8b78a4dbe2c27d14b6679bdc848ce2b) so the shift applies it on the first claim.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: a60d5da2 970b5eb6 fe717512 a9c88fc5.
