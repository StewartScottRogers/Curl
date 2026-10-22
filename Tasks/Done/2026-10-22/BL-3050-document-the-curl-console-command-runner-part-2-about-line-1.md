---
id: BL-3050
title: Document the Curl.Console command runner, part 2 (about line 1800 to 3500)
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-3049]
touches: [Curl.Console/CurlCommandRunner.cs]
model: sonnet
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3050 — Document the Curl.Console command runner, part 2 (about line 1800 to 3500)

## Goal

Every non-obvious type and member in Curl.Console/CurlCommandRunner.cs from the first member after the part 1 range to the end of the member that contains line 3500 says, in an accurate XML doc comment or a short inline comment, what it does and why, so a reader needs no other file to understand it. No behaviour changes.

## Context

Stewart, 2026-10-22: "Fully document and comment all code in each Host." Hosts are Curl.Api and Curl.Api.UnitLibrary, Curl.Console, Libcurl, and Curl.Mcp and Curl.Mcp.UnitLibrary; production code only. Public members already have XML docs (CS1591 is an error), so the work is: (1) add XML docs to internal and private types and members only where the name alone does not say what it does, returns, throws, or why it exists; a doc that merely restates the name is not wanted; (2) correct any existing doc or inline comment that is wrong or vague against the code ("say what it does, do what it says", root CLAUDE.md; terms from Documentation/Wiki/Glossary.md); (3) add inline comments only where the code is not self-evident: why a step exists, or which curl behaviour or upstream file or case it matches. Match the surrounding comment density; no narration of obvious code. Read the project's CLAUDE.md first. Scope is exactly the `touches` list. Tests: Curl.Console.UnitTests.

## Acceptance criteria

- [x] Every internal or private type or member in scope whose name does not fully say what it does has an accurate XML doc comment; none restates its own name.
- [x] Every existing comment in the touched files is true of the code as it is now; any found wrong or vague is corrected.
- [x] New inline comments explain why or name the curl behaviour matched, and none narrates what the next line plainly does.
- [x] The diff changes comments and docs only: no logic, signature, accessibility or public name changed.
- [x] `dotnet build` of the project is clean with warnings as errors, and the fast tests of Curl.Console.UnitTests pass (`dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"`).

## Notes

- Range reviewed: StartInParallelAsync to TransferUploadingAsync. Most members already had accurate docs. Fixed two garbled summaries (TransferInParallelAsync, SwitchesStandardOutputToBinary) that held a stray sentence from another member. Added missing params and returns to WriteOutVariables, a summary to WithMultiOrTimerEvents, and two inline why-comments in TransferUploadingAsync. Comments only.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: a8624aa0 d77f9e20 4a195610 ea3eb31b b928bf9e.
- 2026-10-22: Doing -> Done. Command runner part 2 docs corrected and completed; build and fast tests green
