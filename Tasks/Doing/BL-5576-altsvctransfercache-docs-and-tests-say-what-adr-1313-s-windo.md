---
id: BL-5576
title: AltSvcTransferCache docs and tests say what ADR-1313's Windows answer does
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary/AltSvcTransferCache.cs, Curl.Engine.UnitTests/AltSvcTransferCacheTests.cs, Curl.Engine.UnitTests/AltSvcTransferCacheH3SwitchTests.cs]
requirement: none
created: 2026-11-19
completed:
---
# BL-5576 — AltSvcTransferCache docs and tests say what ADR-1313's Windows answer does

## Goal

Every doc comment and test about alt-svc's no-version-option destinations says what ADR-1313 decided (`h1` alone on Windows, `h1`, `h2` and `h3` elsewhere), and both platform answers are tested on every CI platform.

## Context

Found by the shift-end review of 2026-11-19 (code-reviewer and type-design-analyzer, checked by hand) in the diff d0f316b4ad...b9ca725077, commit for BL-5574 (ADR-1313).

- `Curl.Engine.UnitLibrary/AltSvcTransferCache.cs:39-40`: the class remarks still say an `h3` entry makes a transfer without a version option race HTTP/3 against TCP "as `--http3` does (ADR-1309)"; on Windows that entry is now ignored. CLAUDE.md's "say what it does" rule makes this a defect.
- `Curl.Engine.UnitTests/AltSvcTransferCacheH3SwitchTests.cs:11` (class summary) and `Curl.Engine.UnitTests/AltSvcTransferCacheTests.cs:~184` (`LookupOf_VersionOption_AllowsTheVersionsTheOptionNames` summary) still cite ADR-1309 as if it held on every platform.
- `AltSvcTransferCache.LookupOf(HttpVersion version, bool isWindows = false)` (:266): the default gives the non-Windows answer, so a caller that leaves the argument out silently gets the answer ADR-1313 removed on Windows. `LookupOf_VersionOption_AllowsTheVersionsTheOptionNames` relies on that default.
- `AltSvcTransferCacheTests.cs:131` `ApplyTo_EntryForTheSameHostAndPort_GivesNoRoute` does not pin `onWindows: false`, so on Windows its `h2` entry is filtered out before the same-host check (AF-0314) it is named for; a break in that check would go unseen on Windows.
- `AltSvcTransferCacheH3SwitchTests.cs:21-35` tests the Windows answer through `ApplyTo` only under `[OSCondition(OperatingSystems.Windows)]`, never passing `onWindows: true`, so Linux and macOS CI never exercise it.

## Acceptance criteria

- [ ] `AltSvcTransferCache`'s class remarks say the no-option `h3` race happens off Windows only (ADR-1313).
- [ ] The summaries of `AltSvcTransferCacheH3SwitchTests` and `LookupOf_VersionOption_AllowsTheVersionsTheOptionNames` name ADR-1313's Windows answer.
- [ ] `LookupOf`'s `isWindows` parameter has no default, and every caller passes it.
- [ ] `ApplyTo_EntryForTheSameHostAndPort_GivesNoRoute` passes `onWindows: false`.
- [ ] A test without `[OSCondition]` passes `onWindows: true` and shows an `h2` and an `h3` entry give no route with no version option, so the Windows answer is checked on all three CI platforms.
- [ ] `dotnet build` is clean and `Curl.Engine.UnitTests` fast tests pass; `Curl.Engine.UnitLibrary` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-19: Created by the shift-end review (review-20261119-152921).
- 2026-11-19: Backlog -> Doing. Criteria at claim: e9e9a0a5 e5699396 71a70cb7 51dd886e 13e713cf 9a1dffd3.
