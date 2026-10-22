---
id: BL-3054
title: Review the Curl.Api README against the code
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-3030, BL-3031, BL-3032]
touches: [Curl.Api/README.md, Curl.Api.UnitTests/Readme]
model: sonnet
requirement: none
created: 2026-10-22
completed:
---
# BL-3054 — Review the Curl.Api README against the code

## Goal

Curl.Api/README.md is reviewed against Curl.Api as it is now and corrected, with every claim and code sample true; the README tests still pass.

## Context

Stewart, 2026-10-22: "Fully document and comment all code in each Host." The comment tasks for this project are listed in `depends-on`; write the README from the code as it stands after them. Every statement must be true of the code now, and intent is written as intent (root CLAUDE.md, "Say what it does, do what it says"). Use Documentation/Wiki/Glossary.md terms and read the project's CLAUDE.md. Cross-links to the other host READMEs are added by the final alignment task, so do not chase them here beyond a plain relative link where natural. Tests: Curl.Api.UnitTests.

## Acceptance criteria

- [ ] The README exists in the project folder and states what the project is, how it is used (a short example that compiles or is a command that runs), its folder or type map, and its quality gates and relationship to its sibling projects.
- [ ] Every type, member, option, file and command the README names exists in the code as it is now.
- [ ] If a test reads or checks the README, it still passes; any code sample in it is exercised by that test where the project already does so.
- [ ] No source file other than the README changes, and `dotnet build` of the project is clean with warnings as errors and the fast tests of Curl.Api.UnitTests pass (`dotnet test Curl.Api.UnitTests --filter "TestCategory!=Integration"`).

## Notes

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: cdc198fa fc2f372c 0e267b12 e9da25d9.
