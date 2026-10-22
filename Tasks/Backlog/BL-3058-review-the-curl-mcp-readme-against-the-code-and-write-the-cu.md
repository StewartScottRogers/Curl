---
id: BL-3058
title: Review the Curl.Mcp README against the code and write the Curl.Mcp.UnitLibrary README
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-3052, BL-3053]
touches: [Curl.Mcp/README.md, Curl.Mcp.UnitLibrary/README.md]
model: sonnet
requirement: none
created: 2026-10-22
completed:
---
# BL-3058 — Review the Curl.Mcp README against the code and write the Curl.Mcp.UnitLibrary README

## Goal

Curl.Mcp/README.md is reviewed against the code as it is now and corrected (it is the NuGet package readme, packed by Curl.Mcp.csproj), and Curl.Mcp.UnitLibrary has a README.md saying what it holds (tool contracts, confinement roots, option path tables, results) and how Curl.Mcp uses it.

## Context

Stewart, 2026-10-22: "Fully document and comment all code in each Host." The comment tasks for this project are listed in `depends-on`; write the README from the code as it stands after them. Every statement must be true of the code now, and intent is written as intent (root CLAUDE.md, "Say what it does, do what it says"). Use Documentation/Wiki/Glossary.md terms and read the project's CLAUDE.md. Cross-links to the other host READMEs are added by the final alignment task, so do not chase them here beyond a plain relative link where natural. Tests: Curl.Mcp.UnitTests.

## Acceptance criteria

- [ ] The README exists in the project folder and states what the project is, how it is used (a short example that compiles or is a command that runs), its folder or type map, and its quality gates and relationship to its sibling projects.
- [ ] Every type, member, option, file and command the README names exists in the code as it is now.
- [ ] If a test reads or checks the README, it still passes; any code sample in it is exercised by that test where the project already does so.
- [ ] No source file other than the README changes, and `dotnet build` of the project is clean with warnings as errors and the fast tests of Curl.Mcp.UnitTests pass (`dotnet test Curl.Mcp.UnitTests --filter "TestCategory!=Integration"`).

## Notes

## Log

- 2026-10-22: Created.
