---
id: BL-3020
title: Cover every branch of the manual page generator's Markdown renderer and ASCII layout
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3018]
touches: [Curl.Cli.UnitLibrary/ManualPages/ManualMarkdownRenderer.cs, Curl.Cli.UnitLibrary/ManualPages/ManualAsciiLayout.cs, Curl.Cli.UnitTests/ManualPages/ManualMarkdownRendererTests.cs, Curl.Cli.UnitTests/ManualPages/ManualAsciiLayoutTests.cs]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-3020 — Cover every branch of the manual page generator's Markdown renderer and ASCII layout

## Goal

Every branch of `ManualMarkdownRenderer` and `ManualAsciiLayout` is covered by a focused test, so those two types reach 100% line and branch coverage.

## Context

Split from BL-2881, after BL-3018 commits the generator. `Curl.Cli.UnitTests` has no `InternalsVisibleTo` from `Curl.Cli.UnitLibrary`, so tests reach only public types: make `ManualMarkdownRenderer` and `ManualAsciiLayout` public (they are documented already) and test them directly. Cover the renderer's Markdown constructs (emphasis, code spans, links, lists, quoted lines, the cases that warn) and the layout's wrapping rules: long ASCII examples, and quoted lines that are too wide. Where a rendering must match upstream, take the expected text from the case 1705 and 1706 inputs under `Curl.Cli.UnitTests/ManualPages/Upstream/` or from upstream's `scripts/managen` and `scripts/cd2nroff` at tag `curl-8_22_0`. Measure with `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` and read only the lines for these two files; the other generator types belong to BL-3019 and BL-3021.

## Acceptance criteria

- [x] `ManualMarkdownRendererTests` and `ManualAsciiLayoutTests` exist in `Curl.Cli.UnitTests/ManualPages/`, one test per branch, each named after the behaviour it pins.
- [x] Measure-CodeQuality reports no uncovered line or branch, complexity above 10 or CRAP above 30 in `ManualMarkdownRenderer` or `ManualAsciiLayout`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes for `Curl.Cli.UnitTests`.

## Notes

- `ManualMarkdownRenderer` and `ManualAsciiLayout` are public now, with `ArgumentNullException.ThrowIfNull` on their public methods' collection arguments.
- The first Measure-CodeQuality run had six renderer methods over the complexity gate (coverage counts every branch point) and several `is { Success: true }` matches whose null-check branch no test can reach. I split the renderer into smaller methods (`CloseOpenBlocks`, `IsSkipped`, `HeaderOrTextLine`, `IsFence`, `CloseIndentedQuote`, `Title`, `WarnUnescapedInItem`, `ItemWord`, `OpenTable`, `CheckText`, `AddText`, `AsciiLine`) and test `Match.Success` directly. Behaviour is unchanged: the generator's upstream 1705/1706 tests still pass.
- The text-line "un-escaped < or >" warning ends with two line feeds because `$back` keeps the line's own one, as upstream's `managen` prints it. The test pins that.
- Second Measure-CodeQuality run: no failing member in either type. The 29 that remain in Curl.Cli.UnitLibrary are in the other generator types (BL-3019, BL-3021) and outside ManualPages.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 36eaf022 82109722 48b212cd.
- 2026-10-21: Doing -> Done. Renderer and ASCII layout tests cover every branch; Measure-CodeQuality reports no failing member in either type.
