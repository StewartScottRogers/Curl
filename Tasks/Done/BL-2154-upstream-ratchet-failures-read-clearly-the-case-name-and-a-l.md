---
id: BL-2154
title: Upstream ratchet failures read clearly: the case name and a line diff of what differs
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2157]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-13
completed: 2026-10-13
---
# BL-2154 — Upstream ratchet failures read clearly: the case name and a line diff of what differs

## Goal

A failing upstream case in `UpstreamConformanceTests` shows its upstream name in Test Explorer and CI, and its failure message shows a short expected-against-actual diff of the lines that differ, not only "differs at byte N".

## Context

Stewart, 2026-10-13: he asked whether the vendored upstream cases should become hand-written C# tests. Claude advised keeping them as vendored data (re-vendoring per curl release, which the gap office also runs), and fixing what makes them awkward. Stewart said do all three fixes; this is the first.

Today the data-driven test `UpstreamCase_RunThroughCurl_HoldsTheRatchet(int)` names each row `test<N>` (`UpstreamConformanceTests.cs` around line 62). A failure reads like `<verify><file> (...jar798.txt) differs at byte 145 (line 5): expected "#HttpOnly_localhost\tFALSE\t/...`, which is hard to act on (CI run on 48919fca1, 2026-10-13). Each case's `<info><name>` holds upstream's one-line description. Start at `UpstreamFirstDifference` and `UpstreamCaseVerification` in `Curl.Conformance.UnitLibrary`.

## Acceptance criteria

- [x] Each row's display name is `test<N>: <the case's <name> text>`, trimmed to about 80 characters.
- [x] A failing comparison message names the part (`<verify><stdout>`, `<protocol>`, `<file>` and so on) and shows a few lines of context with the differing expected and actual lines marked; control characters show as visible escapes (`\r`, `\n`, `\t`).
- [x] Unit tests pin the display name and the diff format, including a CRLF-only difference and a missing trailing line.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.

## Notes

- 2026-10-13 (Claude): also put each case's protocols, from its <server> line, in the display name (for example test798 [http]: <name>), and give each row a test category per protocol, so dotnet test --filter "TestCategory=ftp" runs only the FTP cases. Do this after BL-2157 renames the files to .curltest.
- 2026-10-13 (Claude, lane 9): done. `UpstreamCaseDisplayName` names each row `test<N> [<servers>]: <name>` (servers are the first word of each `<client><server>` line; the name is from `<client><name>`, which sits in `<client>`, not `<info>`), cut to 80 characters ending in `...`; a file that does not parse is named `test<N>`. Each row's `TestCategories` holds its servers; `--filter "TestCategory=ftp"` ran 264 rows. `UpstreamFirstDifference.Describe` keeps its first-line sentence unchanged (the gap office's tools and existing tests read "differs at byte N") and appends `UpstreamLineDifference`'s line diff on the following lines: common leading and trailing lines are matched, up to two lines of context each side, expected lines `-`, produced lines `+`, at most five of each then `... N more`, each numbered and quoted with escapes. Chose a prefix/suffix diff over a full LCS diff: a ratchet failure usually differs in one place, and it stays simple and within the complexity gate. Measure-CodeQuality: Curl.Conformance.UnitLibrary 100% line, 100% branch, 0 failing members.

## Log

- 2026-10-13: Created.
- 2026-10-13: Backlog -> Doing.
- 2026-10-13: Doing -> Done. Upstream rows are named test<N> [<servers>]: <name> with a category per server, and a failing comparison shows a numbered, escaped line diff after its first-difference sentence.
