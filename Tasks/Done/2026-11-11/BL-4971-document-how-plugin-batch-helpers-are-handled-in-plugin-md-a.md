---
id: BL-4971
title: Document how plugin batch helpers are handled in Plugin.md and an ADR
priority: High
assignee: Claude
pipeline: docs
depends-on: [BL-4977]
touches: [Documentation/Conformance/Extended/Plugin.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-11-11
completed: 2026-11-11
---
# BL-4971 — Document how plugin batch helpers are handled in Plugin.md and an ADR

## Goal

`Documentation/Conformance/Extended/Plugin.md` Discovery-3 states how `.cmd` and `.bat` plugin helpers are handled, and an ADR records the choice.

## Context

BL-4977 refuses a URL holding `& | < > ^ " %` or a control character when the helper is a `.cmd` or `.bat` file (BatBadBut, CVE-2024-24576), instead of launching through an escaped `cmd.exe /d /s /c`, because cmd escaping cannot be made reliable. Read `Curl.Extended.Protocol.Plugin.UnitLibrary/SystemHelperProcessStarter.cs` after BL-4977 for the exact character set and error text.

## Acceptance criteria

- [x] Plugin.md Discovery-3 states that batch helpers get the URL as one argument only when it holds no cmd metacharacter, and otherwise the transfer is refused, naming the characters and the error.
- [x] A new ADR, marked "Decided by Claude under Stewart's delegation", records the choice and why (refuse versus escape), with its row in `Documentation/Planning/Decisions/README.md`; the number is the next free one at the time.
- [x] Every statement added matches the code as it is.

## Notes

Split from BL-4898 (over its cost cap).

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: 8fd3c890 6402073e 71dcc8d2.
- 2026-11-11: Doing -> Done. Plugin.md Discovery-3 and ADR-1248 document the batch helper refusal
