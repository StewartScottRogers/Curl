---
id: BL-4373
title: Extension: Land the WebDavTools list and sync code (build and fast tests)
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-4091, BL-4093, BL-4094, BL-4303, BL-4304, BL-4305]
touches: [Curl.Extension.WebDavTools.UnitLibrary, Curl.Extension.WebDavTools.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionOptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionTransferHookCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-30
completed:
---
# BL-4373 — Extension: Land the WebDavTools list and sync code (build and fast tests)

## Goal

`--webdav-list` and `--webdav-sync <dir>` (extension `webdav-tools`, ADR-1149) are committed, with the library building clean and the fast tests green.

## Context

Split from BL-4131, whose code was finished but not committed when the shift deadline came. The stash holds the library, its tests, the catalogue registrations, the slnx entry, ADR-1149 and its README row. Design: ADR-1149, RFC 4918. Quality gates are the next task, BL-4374.

## Acceptance criteria

- [ ] `Curl.Extension.WebDavTools.UnitLibrary` and `Curl.Extension.WebDavTools.UnitTests` exist in `/Extensions/WebDavTools/` in `Curl.slnx`, registered in the option, transfer-hook and description catalogues, and `VerifyExtensionIsolation` passes.
- [ ] ADR-1149 exists, is marked "Decided by Claude under Stewart's delegation", and has its row in `Documentation/Planning/Decisions/README.md`.
- [ ] `dotnet build` is clean.
- [ ] The fast tests pass, including the WebDavTools tests (about 99) and the `CURL_EXTENSIONS=0` test that refuses `--webdav-list` with exit 2.
- [ ] The work is committed.

## Notes

Split from BL-4131 (over its cost cap). Continues the stashed work of BL-4131.

Run no `Measure-CodeQuality.ps1` here: it took 64 minutes for Composition. BL-4374 does the WebDavTools run.

## Log

- 2026-10-30: Created.
- 2026-10-30: Backlog -> Doing. Criteria at claim: e93d9b26 81e106cc 42b24d12 a7e7b52e 1d40d7f1.
