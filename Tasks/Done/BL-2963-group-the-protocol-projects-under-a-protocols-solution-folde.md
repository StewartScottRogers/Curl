---
id: BL-2963
title: Group the protocol projects under a Protocols solution folder
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.slnx, CLAUDE.md, .claude/skills/new-project/SKILL.md, Documentation/Planning/Decisions/README.md]
lane: no
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2963 — Group the protocol projects under a Protocols solution folder

## Goal

Solution Explorer shows one `Protocols` solution folder holding one folder per protocol, each with
that protocol's library and test projects.

## Context

Stewart, 2026-10-21: "put all the Protocols and their tests in a solution folder by the protocol
name under one solution folder called Protocols". The solution lists 100 projects as one flat run;
35 of them are `Curl.Protocol.*`. Solution folders change only `Curl.slnx`: the directories on disk
stay flat, so namespaces, builds, tests and CI are unchanged. ADR-0731 records the rule.

## Acceptance criteria

- [x] `Curl.slnx` has a `/Protocols/` folder with one `/Protocols/<Name>/` folder per
      `Curl.Protocol.<Name>.*` project group (Abstractions included), and no `Curl.Protocol.*`
      project outside it.
- [x] `CLAUDE.md` and the `new-project` skill say protocol projects go in their
      `/Protocols/<Name>/` folder and every other project stays in the flat run.
- [x] `dotnet build Curl.slnx` succeeds and `dotnet sln Curl.slnx list` lists all 100 projects.

## Notes

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Interactive session. Criteria at claim: eaaeded8 853b7604 99ab2da6.
- 2026-10-21: Doing -> Done. Curl.slnx groups the 35 protocol projects in 17 Protocols/<Name> folders; build clean, 100 projects listed, Abstractions tests 877/877.
