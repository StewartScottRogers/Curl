---
id: BL-3045
title: Document the Curl.Console output and input stream wrappers
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Console/ClosedStandardOutputStream.cs, Curl.Console/DeferredOutputFileStream.cs, Curl.Console/DumpHeaderOutputStream.cs, Curl.Console/EtagSaveStream.cs, Curl.Console/FlushEachWriteStream.cs, Curl.Console/HoldableStream.cs, Curl.Console/LineFeedToCrLfStream.cs, Curl.Console/RateLimitedUploadStream.cs, Curl.Console/RemoteHeaderNameStream.cs, Curl.Console/StandardInputUploadStream.cs, Curl.Console/StandardOutputFailureDeferringStream.cs, Curl.Console/TerminalBinaryOutputRefusingStream.cs, Curl.Console/TextModeUntilBinaryStream.cs, Curl.Console/WriteGate.cs, Curl.Console/WriteGateStream.cs, Curl.Console/StandardOutputVirtualTerminal.cs, Curl.Console/StandardOutputOpener.cs]
model: sonnet
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3045 — Document the Curl.Console output and input stream wrappers

## Goal

Every non-obvious type and member in the Curl.Console stream wrappers and output openers (ClosedStandardOutputStream through StandardOutputOpener: the *Stream.cs files, WriteGate, StandardOutputVirtualTerminal, StandardOutputOpener) says, in an accurate XML doc comment or a short inline comment, what it does and why, so a reader needs no other file to understand it. No behaviour changes.

## Context

Stewart, 2026-10-22: "Fully document and comment all code in each Host." Hosts are Curl.Api and Curl.Api.UnitLibrary, Curl.Console, Libcurl, and Curl.Mcp and Curl.Mcp.UnitLibrary; production code only. Public members already have XML docs (CS1591 is an error), so the work is: (1) add XML docs to internal and private types and members only where the name alone does not say what it does, returns, throws, or why it exists; a doc that merely restates the name is not wanted; (2) correct any existing doc or inline comment that is wrong or vague against the code ("say what it does, do what it says", root CLAUDE.md; terms from Documentation/Wiki/Glossary.md); (3) add inline comments only where the code is not self-evident: why a step exists, or which curl behaviour or upstream file or case it matches. Match the surrounding comment density; no narration of obvious code. Read the project's CLAUDE.md first. Scope is exactly the `touches` list. Tests: Curl.Console.UnitTests.

## Acceptance criteria

- [x] Every internal or private type or member in scope whose name does not fully say what it does has an accurate XML doc comment; none restates its own name.
- [x] Every existing comment in the touched files is true of the code as it is now; any found wrong or vague is corrected.
- [x] New inline comments explain why or name the curl behaviour matched, and none narrates what the next line plainly does.
- [x] The diff changes comments and docs only: no logic, signature, accessibility or public name changed.
- [x] `dotnet build` of the project is clean with warnings as errors, and the fast tests of Curl.Console.UnitTests pass (`dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"`).

## Notes

- Audited all 17 files; 7 needed doc changes (field docs, Flush rationale, two corrected summaries), the rest were already accurate. Build clean, 2825 fast tests pass.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: a8624aa0 d77f9e20 4a195610 ea3eb31b b928bf9e.
- 2026-10-22: Doing -> Done. Console stream wrapper docs audited and completed
