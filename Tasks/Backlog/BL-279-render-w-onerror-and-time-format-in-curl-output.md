---
id: BL-279
title: Render -w %{onerror} and %time{format} in Curl.Output
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-224]
touches: [Curl.Output.UnitLibrary, Curl.Output.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-279 — Render -w %{onerror} and %time{format} in Curl.Output

## Goal

`WriteOutTemplateRenderer` stops at `%{onerror}` after a successful transfer and renders `%time{format}` as curl 8.21.0 does.

## Context

- Found by BL-224. curl 8.21.0 renders `-w "%time{%Y}Q"` as the year then `Q` (measured 2026-09-26); today the renderer writes `%time{%Y}Q` literally, because BL-224 covered only the directives it named.
- `%{onerror}` ends the output when the transfer succeeded (curl `src/tool_writeout.c`, `VAR_ONERROR`); the renderer needs to know whether the transfer failed, which `IWriteOutVariableSource` does not yet say.
- %time needs an injected `TimeProvider`; measure the strftime subset curl supports, including `%f` for microseconds, before pinning.

## Acceptance criteria

- [ ] `%{onerror}` after a successful transfer writes nothing further and after a failed one continues (measured on curl 8.21.0, pinned in a test).
- [ ] `%time{...}` output for the measured formats matches curl 8.21.0 with a fake `TimeProvider`.
- [ ] `dotnet build Curl.Output.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Output`.

## Notes

## Log

- 2026-09-26: Created.
