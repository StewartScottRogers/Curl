---
id: BL-2066
title: Cover RedirectFollower.EncodedCharacter's last branch and bring MultipartPartHeaders.ChooseContentType under the complexity gate
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-10-11
completed: 2026-10-11
---
# BL-2066 — Cover RedirectFollower.EncodedCharacter's last branch and bring MultipartPartHeaders.ChooseContentType under the complexity gate

## Goal

`Measure-CodeQuality.ps1` reports every member of Curl.Core.UnitLibrary inside the gates, so the library shows `pass` on the coverage page.

## Context

The coverage page (<https://stewartscottrogers.github.io/Curl/coverage/>, measured 2026-10-11 from commit 53f17a0) lists two members of Curl.Core.UnitLibrary outside a gate:

- `RedirectFollower.EncodedCharacter(char, bool)` (`Curl.Core.UnitLibrary/RedirectFollower.cs:463`): branch coverage 87.5%. One branch is not taken by any fast test; it is the only uncovered branch in the solution (27,875 of 27,876). Add the test that takes it, or remove the branch if it cannot be reached.
- `MultipartPartHeaders.ChooseContentType` (`Curl.Core.UnitLibrary/Multipart/MultipartPartHeaders.cs:105`): complexity 12; the gate is 10. The build's `CA1502` passes it, because the page uses the coverage report's complexity, which counts the compiled method. Split it into smaller, well-named methods that stay at 10 or below by that measure.

Behaviour does not change.

## Acceptance criteria

- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary` reports no member outside a gate, at 100% line and 100% branch coverage.
- [x] No existing test was changed or removed to get there; any new member has its own tests.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Also split UrlSchemeGuesser.HasScheme(string, bool) (compiled complexity 14) with IsWindowsDrivePrefix; it surfaced in the same measurement. Behaviour unchanged. Added one EncodedForFollow row (character above Latin-1) for the missing branch.

## Log

- 2026-10-11: Created.
- 2026-10-11: Backlog -> Doing.
- 2026-10-11: Doing -> Done. Branch covered, ChooseContentType and HasScheme split; Core library 0 failing members
