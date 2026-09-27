---
id: BL-258
title: Record the -F form-parsing decisions of BL-189 in an ADR
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-09-26
completed:
---
# BL-258 — Record the -F form-parsing decisions of BL-189 in an ADR

## Goal

An ADR in `Documentation/Planning/Decisions` records the four behaviour decisions BL-189 made when it ported curl 8.21.0's `-F` parser into `Curl.Cli.UnitLibrary`.

## Context

- BL-189 could not write the ADR itself: `Documentation/Planning/Decisions` was held by BL-053 in another dark factory lane, so the decisions were recorded in BL-189's Notes instead. Copy them from there.
- Code: `Curl.Cli.UnitLibrary/MultipartFormField.cs`, `FormPartParameterReader.cs`, `FormPartSpecification.cs`; tests in `Curl.Cli.UnitTests/CommandLineFormOptionTests.cs`. Upstream: `src/tool_formparse.c` at tag `curl-8_21_0`.
- The four decisions:
  1. `-F name=@file`, `name=<file`, `@-` and `<-` name their file (or standard input) in the part specification and read nothing while parsing. curl reads a non-regular standard input at parse time; the read moves to the transfer layer, which sends the same bytes.
  2. A `;headers=@file` file that cannot be read always warns `Cannot read from <file>: No such file or directory`, because `IDataFileReader` does not say why a read failed; curl prints `strerror(errno)`, so a directory or a denied file reads differently there.
  3. A `;headers=@file` file is decoded as UTF-8 and split at LF, not read through the C runtime's text mode, so a Ctrl-Z byte does not end it early and a line longer than curl's 8192-byte buffer is kept whole rather than failing the read.
  4. `-F` together with a `-d`/`--data*`/`--json` body is refused once the whole command line is read, as `CommandLineRefusal.FormAndDataBoth` with no lines of its own after the request-method warning, because curl 8.21.0 reports it after the no-URL check and without the `curl: option ...` and try-help lines.

## Acceptance criteria

- [ ] A new ADR in `Documentation/Planning/Decisions`, numbered after the highest existing one and listed in that folder's `README.md`, states the four decisions above, each with its reason, and is marked "Decided by Claude under Stewart's delegation".

## Notes

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
