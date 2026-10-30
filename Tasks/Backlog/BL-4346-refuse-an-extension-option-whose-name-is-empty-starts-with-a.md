---
id: BL-4346
title: Refuse an extension option whose name is empty, starts with a dash or holds an equals sign at composition
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extension.Composition.UnitLibrary/ExtensionCommandLineOptions.cs, Curl.Extension.Composition.UnitTests]
requirement: none
created: 2026-10-30
completed:
---
# BL-4346 — Refuse an extension option whose name is empty, starts with a dash or holds an equals sign at composition

## Goal

Composition refuses an extension option whose name the parser could never match, with a clear `InvalidOperationException`, so no extension option is silently dead. Such names are null or empty, start with `-`, or contain `=`.

## Context

Found by the type-design-analyzer reviewer.

- `Curl.Extension.Abstractions.UnitLibrary/ExtensionOption.cs:12` is a bare record.
- `ExtensionCommandLineOptions.Checked` (`Curl.Extension.Composition.UnitLibrary/ExtensionCommandLineOptions.cs:40-55`) only rejects duplicates and curl's own names.
- `CommandLineParser` splits on `=` before it looks up `ExtensionOptions`.
- An option registered as `"--cache-dir"`, or with an `=` in its name, therefore passes composition, but the command line can never select it. The user gets "unknown option" for an option the extension claims to add.
- A null name throws a raw `NullReferenceException` at `byName.TryAdd`.

## Acceptance criteria

- [ ] `ExtensionCommandLineOptions.Checked` throws `InvalidOperationException`, naming the option, for a null, empty, dash-led or `=`-containing name.
- [ ] Unit tests in `Curl.Extension.Composition.UnitTests` cover each of the four rejected shapes, plus one valid name that still passes.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-30: Created by the shift-end review (ADR-0710) of a9f1a8149..b79175cc5.
