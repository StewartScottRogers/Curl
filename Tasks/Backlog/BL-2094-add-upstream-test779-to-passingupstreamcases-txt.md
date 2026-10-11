---
id: BL-2094
title: Add upstream test779 to PassingUpstreamCases.txt
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-2090]
touches: [Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-11
completed:
---
# BL-2094 — Add upstream test779 to PassingUpstreamCases.txt

## Goal

Upstream test779 is listed in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` and passes in process.

## Context

BL-2090 made Curl send nothing after `CAPABILITY` and exit 67 after an HTTP redirect to IMAP on another host with `--oauth2-bearer` (curl 8.21.0 `tests/data/test779`). Its lane could not edit `PassingUpstreamCases.txt` because BL-2097 held `Curl.Conformance.UnitTests` at the time.

## Acceptance criteria

- [ ] `test779` is in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` and the conformance tests that read it pass; if it does not pass in process, the remaining cause is filed as a task.
- [ ] `dotnet build` is clean and the fast tests are green.

## Notes

## Log

- 2026-10-11: Created.
