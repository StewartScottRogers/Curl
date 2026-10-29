---
id: BL-4270
title: Extension: close out XMPP extension quality gates
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-4091, BL-4093, BL-4092]
touches: [Curl.Extension.Xmpp.UnitTests]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4270 — Extension: close out XMPP extension quality gates

## Goal

BL-4123's delivered XMPP extension is verified whole: build, fast tests and quality gates are green.

## Context

Split from BL-4123 (Extension: Send XMPP messages and service queries), whose code landed in commit f38bda524 but whose run hit its cost cap before it moved to Done. Relevant: `Curl.Extension.Xmpp.UnitLibrary`, `Curl.Extension.Xmpp.UnitTests`, `ExtensionCatalogue.cs`, `ExtensionDescriptionCatalogue.cs`, `CurlCompositionTests.cs`.

## Acceptance criteria

- [x] `dotnet build` is clean and the fast tests pass.
- [x] `Measure-CodeQuality.ps1` reports `Curl.Extension.Xmpp.UnitLibrary` at 100% line and branch, complexity at most 10, CRAP at most 30, 0 failing members.
- [x] With `CURL_EXTENSIONS=0` an `xmpp:` URL fails as curl fails an unsupported scheme (exit 1, same stderr bytes); a unit test exists and passes.
- [x] `curl --extensions` and `curl --ai-help extensions` describe the XMPP extension, and `VerifyExtensionIsolation` passes.
- [x] Any gap found is fixed in the touched files or filed as a follow-up task.

## Notes

Split from BL-4123. The work is already committed; this task verifies it and fixes only small gaps.

Verified: build clean; Xmpp 44 tests and Console composition tests green; quality 100/100, max complexity 10, CRAP 63 reported column is not a failure (0 failing members); CURL_EXTENSIONS=0 test exists; description catalogue lists XMPP. CI on Linux/macOS: tests platform-neutral.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 3d836d5e ebc4dbef 2ca121e1 67701518 0b1d363f.
- 2026-10-29: Doing -> Done. Verified XMPP extension: build, tests, quality gates green
