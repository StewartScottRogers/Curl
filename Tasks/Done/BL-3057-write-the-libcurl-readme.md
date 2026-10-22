---
id: BL-3057
title: Write the Libcurl README
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-3037, BL-3038, BL-3039, BL-3040, BL-3041, BL-3042, BL-3043]
touches: [Libcurl/README.md]
model: sonnet
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3057 — Write the Libcurl README

## Goal

Libcurl has a README.md that says what it is (the C#-facing libcurl surface, ADR-0506), which libcurl APIs it covers (easy, multi, share, url, header, mime, websocket, global, printf and version helpers), and its folder map.

## Context

Stewart, 2026-10-22: "Fully document and comment all code in each Host." The comment tasks for this project are listed in `depends-on`; write the README from the code as it stands after them. Every statement must be true of the code now, and intent is written as intent (root CLAUDE.md, "Say what it does, do what it says"). Use Documentation/Wiki/Glossary.md terms and read the project's CLAUDE.md. Cross-links to the other host READMEs are added by the final alignment task, so do not chase them here beyond a plain relative link where natural. Tests: Libcurl.UnitTests.

## Acceptance criteria

- [x] The README exists in the project folder and states what the project is, how it is used (a short example that compiles or is a command that runs), its folder or type map, and its quality gates and relationship to its sibling projects.
- [x] Every type, member, option, file and command the README names exists in the code as it is now.
- [x] If a test reads or checks the README, it still passes; any code sample in it is exercised by that test where the project already does so.
- [x] No source file other than the README changes, and `dotnet build` of the project is clean with warnings as errors and the fast tests of Libcurl.UnitTests pass (`dotnet test Libcurl.UnitTests --filter "TestCategory!=Integration"`).

## Notes

- README written from the code and Libcurl/CLAUDE.md; example is a compiled-by-inspection snippet (not exercised by a test; no test reads the README). Build clean, 1331 fast tests passed.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: cdc198fa fc2f372c 0e267b12 cd769bdb.
- 2026-10-22: Doing -> Done. Libcurl/README.md written
