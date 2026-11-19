---
id: BL-5528
title: Name Curl.Api.Options.Authentication in the Curl.Api README so HttpAuthenticationMethods samples compile
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-5517]
touches: [Curl.Api/README.md]
requirement: none
created: 2026-11-18
completed:
---
# BL-5528 — Name Curl.Api.Options.Authentication in the Curl.Api README so HttpAuthenticationMethods samples compile

## Goal

A reader who copies `Curl.Api/README.md`'s samples and sets an authentication method such as Digest with `HttpAuthenticationMethods` gets a program that compiles, because the README names `Curl.Api.Options.Authentication`.

## Context

From bug report `2026-11-18T1734-Curl-bug-report.md` (new finding 1, Low), found while writing Part 2 of the *Surly* cookbook, tested at `f4b30d981f`.

A reader who sets an authentication method uses `HttpAuthenticationMethods`; plain-HTTP servers need Digest because Basic is refused there. That type lives in `Curl.Api.Options.Authentication`. Neither the README's `<!-- sample: usings -->` block (`System.Net`, `Curl.Api`, `Curl.Api.Errors`, `Curl.Api.Options.Http`) nor its text names that namespace, so the program does not compile until the reader finds it.

This is the same kind of gap as BL-5517 (`Curl.Api.Codes` for `CurlCode`), which edits the same usings sample, so this task waits for it. While here, check every other public `Curl.Api` type the README's samples use or tell the reader to use, and name its namespace too. `Curl.Api.UnitTests/Readme/ReadmeTests.cs` checks the README's samples; keep it passing.

## Acceptance criteria

- [ ] The `<!-- sample: usings -->` block in `Curl.Api/README.md` includes `using Curl.Api.Options.Authentication;`, or the README names that namespace where `HttpAuthenticationMethods` is first used or recommended.
- [ ] Every other public `Curl.Api` type the README's samples use is in a namespace the usings sample or the text names.
- [ ] `Curl.Api.UnitTests/Readme/ReadmeTests.cs` passes.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-18: Created from bug report 2026-11-18T1734-Curl-bug-report.md, new finding 1.
