---
id: BL-6004
title: Make dotnet format pass in the Engine, Networking and Conformance projects
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitTests, Curl.Engine.IntegrationTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Conformance.Differential.UnitLibrary, Curl.Conformance.Differential.IntegrationTests, Curl.Conformance.SshServer.UnitLibrary, Curl.Conformance.SshServer.UnitTests, Curl.Conformance.UnitProgram.UnitLibrary, Curl.Conformance.Extended.UnitTests, Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests, Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-11-26
completed:
---
# BL-6004 — Make dotnet format pass in the Engine, Networking and Conformance projects

## Goal

`dotnet format Curl.slnx --verify-no-changes --include <each path in touches>` exits 0: the `using` order, whitespace and final newlines in this task's touches follow `.editorconfig`.

## Context

Part of BL-5923, split by project group so lanes run the parts in parallel. On 2026-11-26 `dotnet format` reported 680 files across 53 projects: 636 IMPORTS (`using` order), 31 FINALNEWLINE and 13 WHITESPACE. Line endings are left to git (ADR-1417), so there are no ENDOFLINE fixes. Run `dotnet format Curl.slnx --include <each path in touches>`, and change nothing outside touches. It is a formatting-only change; if `dotnet format` wants to change code beyond `using` order and whitespace, leave that file alone and say why under Notes.

## Acceptance criteria

- [ ] `dotnet format Curl.slnx --verify-no-changes --include <each path in touches>` exits 0.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing. Criteria at claim: 03521b86 3d836d5e.
