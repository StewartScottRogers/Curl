---
id: BL-3037
title: Document the Libcurl option table and setter code
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Libcurl/Options/CurlEasyOption.cs, Libcurl/Options/CurlEasyOptionFlags.cs, Libcurl/Options/CurlOption.cs, Libcurl/Options/CurlOptionType.cs, Libcurl/Options/CurlOptionValues.cs, Libcurl/Options/CurlOptions.cs, Libcurl/Options/EasyOptionDispatch.cs, Libcurl/Options/EasyOptionSetter.cs, Libcurl/Options/EasyOptionSetters.cs]
model: sonnet
requirement: none
created: 2026-10-22
completed:
---
# BL-3037 — Document the Libcurl option table and setter code

## Goal

Every non-obvious type and member in the nine files directly in Libcurl/Options (CurlEasyOption.cs through EasyOptionSetters.cs) says, in an accurate XML doc comment or a short inline comment, what it does and why, so a reader needs no other file to understand it. No behaviour changes.

## Context

Stewart, 2026-10-22: "Fully document and comment all code in each Host." Hosts are Curl.Api and Curl.Api.UnitLibrary, Curl.Console, Libcurl, and Curl.Mcp and Curl.Mcp.UnitLibrary; production code only. Public members already have XML docs (CS1591 is an error), so the work is: (1) add XML docs to internal and private types and members only where the name alone does not say what it does, returns, throws, or why it exists; a doc that merely restates the name is not wanted; (2) correct any existing doc or inline comment that is wrong or vague against the code ("say what it does, do what it says", root CLAUDE.md; terms from Documentation/Wiki/Glossary.md); (3) add inline comments only where the code is not self-evident: why a step exists, or which curl behaviour or upstream file or case it matches. Match the surrounding comment density; no narration of obvious code. Read the project's CLAUDE.md first. Scope is exactly the `touches` list. Tests: Libcurl.UnitTests.

## Acceptance criteria

- [ ] Every internal or private type or member in scope whose name does not fully say what it does has an accurate XML doc comment; none restates its own name.
- [ ] Every existing comment in the touched files is true of the code as it is now; any found wrong or vague is corrected.
- [ ] New inline comments explain why or name the curl behaviour matched, and none narrates what the next line plainly does.
- [ ] The diff changes comments and docs only: no logic, signature, accessibility or public name changed.
- [ ] `dotnet build` of the project is clean with warnings as errors, and the fast tests of Libcurl.UnitTests pass (`dotnet test Libcurl.UnitTests --filter "TestCategory!=Integration"`).

## Notes

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: a8624aa0 d77f9e20 4a195610 ea3eb31b 3f9f1d6f.
