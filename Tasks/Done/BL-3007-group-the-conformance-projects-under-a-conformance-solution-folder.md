---
id: BL-3007
title: Group the conformance projects under a Conformance solution folder
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.slnx, CLAUDE.md, .claude/skills/new-project/SKILL.md, Documentation/Planning/Decisions/ADR-0731-protocol-projects-sit-in-a-protocols-solution-folder.md]
lane: no
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-3007 — Group the conformance projects under a Conformance solution folder

## Goal

Solution Explorer shows one `Conformance` solution folder holding one folder per conformance part,
each with that part's library and test projects.

## Context

Stewart, 2026-10-21: "Can we create a solution folder and put all the conformance projects in it",
with one subfolder per part, as for the protocols (BL-2963, ADR-0731).

## Acceptance criteria

- [x] `Curl.slnx` has `/Conformance/` with `/Harness/` (Curl.Conformance.UnitLibrary and .UnitTests),
      `/Http2Server/`, `/Http3Server/`, `/LibTest/`, `/SshServer/` and `/UnitProgram/`, and no
      `Curl.Conformance.*` project outside it.
- [x] CLAUDE.md, the `new-project` skill and an amendment to ADR-0731 say so.
- [x] `dotnet build Curl.slnx` succeeds and the solution lists the same projects as before.

## Notes

## Log

- 2026-10-21: Created, worked and moved to Done in one interactive session.
