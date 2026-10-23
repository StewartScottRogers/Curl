---
id: BL-3152
title: Correct CommandLineRefusal's doc comments and the ADR-0762 index row for bare curl's --ai-help try-help line
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Cli.UnitLibrary/CommandLineRefusal.cs, Documentation/Wiki/Glossary.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3152 — Correct CommandLineRefusal's doc comments and the ADR-0762 index row for bare curl's --ai-help try-help line

## Goal

`CommandLineRefusal`'s doc comments, the glossary's "Try-help line" entry and the ADR index row for ADR-0762 say what the code does now that bare `curl` prints `TryHelpLineNamingAiHelp`.

## Context

Found by the shift-end review of 2026-10-22 (ADR-0710), reviewers type-design-analyzer and code-reviewer, in the change from commit 700a866c2 (BL-3145, ADR-0762).

- `Curl.Cli.UnitLibrary/CommandLineRefusal.cs:8-9`: the class summary still says the refusal is "`TryHelpLine` alone for `EmptyCommandLine`". `EmptyCommandLine` now prints `TryHelpLineNamingAiHelp`.
- `Curl.Cli.UnitLibrary/CommandLineRefusal.cs:24`: `TryHelpLine`'s summary says "The last line of every refusal, exactly as curl prints it". That is no longer true of `EmptyCommandLine`. Code that trusts it (for example `StandardErrorLines[^1] == TryHelpLine`) gets the wrong answer for bare `curl`.
- `Curl.Cli.UnitLibrary/CommandLineRefusal.cs:20`: the remark says the texts were checked byte for byte against curl 8.21.0. It does not mention the one line that deliberately differs (ADR-0762).
- `Documentation/Wiki/Glossary.md:23`: the "Try-help line" entry does not mention the empty-command-line variant.
- `Documentation/Planning/Decisions/README.md:795`: the ADR-0762 row has two cells. The table has four (`ADR | Title | Status | Date`), so Status and Date render empty.

CLAUDE.md's "Say what it does, do what it says" rule counts each of these as a defect.

## Acceptance criteria

- [x] The class summary of `CommandLineRefusal` names `TryHelpLineNamingAiHelp` as the only line of `EmptyCommandLine`, and cites ADR-0762.
- [x] `TryHelpLine`'s summary says it is the last line of every refusal except `EmptyCommandLine`.
- [x] The `<remarks>` say that `TryHelpLineNamingAiHelp` is the one deliberate difference from curl's bytes (ADR-0762).
- [x] The Glossary's "Try-help line" entry mentions the variant that names `--ai-help` and is printed for an empty command line.
- [x] The ADR-0762 row in `Documentation/Planning/Decisions/README.md` has four cells, ending `| Accepted | 2026-10-22 |`. That status matches the ADR's own Status line.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Comment and document edits only (no code change), so no Measure-CodeQuality run; build clean, Curl.Cli.UnitTests 4115 passed.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 512cd130 ea4252d7 aa44bc58 6237b2f8 64b4d5af 3d836d5e.
- 2026-10-22: Doing -> Done. CommandLineRefusal doc comments, Glossary entry and ADR-0762 index row now match bare curl's --ai-help try-help line
