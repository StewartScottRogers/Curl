---
id: BL-5024
title: Match structured_field write-out names case-insensitively and never truncate -w on an unknown long name
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Output.UnitLibrary/WriteOutTemplateRenderer.cs, Curl.Extension.StructuredFields.UnitLibrary/StructuredFieldsTransferHook.cs, Curl.Output.UnitTests/StructuredFieldWriteOutTests.cs, Curl.Output.UnitTests/WriteOutTemplateRendererTests.cs, Curl.Extension.StructuredFields.UnitTests/StructuredFieldsTransferHookTests.cs]
requirement: none
created: 2026-11-12
completed:
---
# BL-5024 — Match structured_field write-out names case-insensitively and never truncate -w on an unknown long name

## Goal

`curl --structured-fields -w "%{structured_field.Priority}"` (and `Cache-Status`, `PRIORITY`) prints the parsed field value and the rest of the `-w` format, exactly as the lower-case name does.

## Context

From the outside fitness review of 2026-11-12 (commit 2d2ad7f8b, `preview` curl-win-x64.zip), finding EXT-010 (Medium, Confirmed). Carried over from the 2026-11-11 review, where BL-4919 (Done 2026-11-11) fixed lower-case names only; mixed- and upper-case names are still open. Data loss: the `-w` output is silently truncated.

**Repro.** Run an HTTP server on 127.0.0.1:48000 whose `/sf` route answers 200 with `Cache-Status: ExampleCache; hit; ttl=30` and `Priority: u=1, i`. Run `curl -s --structured-fields http://127.0.0.1:48000/sf -o NUL -w "A[%{structured_field.Priority}]B\n"`. Repeat with `Cache-Status`, and with `PRIORITY`.

**Expected.** Field names are case-insensitive (RFC 9110 section 5.1), and the `--structured-fields` help names `%{structured_field.<name>}`. Output is `A[Dictionary of 2: u=Integer 1; i=Boolean true]B`, which is what `structured_field.priority` prints.

**Actual.** stdout is exactly `A[`, no stderr, exit 0. Without `--structured-fields`, even the lower-case name truncates the output. An unknown short variable such as `%{nosuchvar}` warns and continues.

**Where to start.** `Curl.Output.UnitLibrary/WriteOutTemplateRenderer.cs`, `RenderVariableAsync` (about line 337): a name of `VariableNameBufferBytes` (24) bytes or more that is not a dotted name `variables.TryGetVariableText` knows jumps to the end of the template. The lookup is case-sensitive; the values are set in `Curl.Extension.StructuredFields.UnitLibrary`. Earlier fix: `Curl.Output.UnitTests/StructuredFieldWriteOutTests.cs`.

## Acceptance criteria

- [ ] `StructuredFieldWriteOutTests` (in `Curl.Output.UnitTests`) has tests that `%{structured_field.Priority}`, `%{structured_field.PRIORITY}` and `%{structured_field.Cache-Status}` render the same text as the lower-case name (`Dictionary of 2: u=Integer 1; i=Boolean true`; `List of 1: Token ExampleCache [hit=Boolean true, ttl=Integer 30]`) and that the text after the variable (`]B`, `]C`) is still printed.
- [ ] A test in `StructuredFieldWriteOutTests` shows `%{structured_field.<name>}` with a name longer than 24 bytes that is not present prints nothing for the variable and the rest of the template is still printed (no truncation), with or without `--structured-fields`.
- [ ] A test in `WriteOutTemplateRendererTests.cs` keeps `%{nosuchvar}` (unknown short name) warning and continuing, and a long unknown name without a dot behaving as before.
- [ ] The whole acceptance output holds: `-w "A[%{structured_field.Priority}]B[%{structured_field.Cache-Status}]C\n"` prints `A[Dictionary of 2: u=Integer 1; i=Boolean true]B[List of 1: Token ExampleCache [hit=Boolean true, ttl=Integer 30]]C`, covered by a test in `Curl.Extension.StructuredFields.UnitTests/StructuredFieldsTransferHookTests.cs`.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). The unit tests need no server. Which file holds the case-insensitive match (renderer or the hook that sets the values) is the implementer's call; the `touches` name both candidates.

## Log

- 2026-11-12: Created.
