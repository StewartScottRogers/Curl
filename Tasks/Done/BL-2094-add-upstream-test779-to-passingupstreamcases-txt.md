---
id: BL-2094
title: Add upstream test779 to PassingUpstreamCases.txt
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-2090]
touches: [Curl.Conformance.UnitTests, Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-10-11
completed: 2026-10-11
---
# BL-2094 — Add upstream test779 to PassingUpstreamCases.txt

## Goal

Upstream test779 is listed in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` and passes in process.

## Context

BL-2090 made Curl send nothing after `CAPABILITY` and exit 67 after an HTTP redirect to IMAP on another host with `--oauth2-bearer` (curl 8.21.0 `tests/data/test779`). Its lane could not edit `PassingUpstreamCases.txt` because BL-2097 held `Curl.Conformance.UnitTests` at the time.

## Acceptance criteria

- [x] `test779` is in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` and the conformance tests that read it pass; if it does not pass in process, the remaining cause is filed as a task.
- [x] `dotnet build` is clean and the fast tests are green.

## Notes

- 2026-10-11: Adding 779 failed with `B002 LOGIN v` still sent: an HTTP first URL has no `MailRequestOptions` (the mapping returns null for non-mail schemes), so BL-2090's `HopMail` never marked the token withheld. Fixed in `RedirectFollower.HopMail`, which now also reads `Http.BearerToken` when `Mail` is null. Added Curl.Core.UnitLibrary and Curl.Core.UnitTests to `touches`; no other Doing task on origin/work/dark-factory names them.

## Log

- 2026-10-11: Created.
- 2026-10-11: Backlog -> Doing.
- 2026-10-11: Doing -> Done. test779 listed; RedirectFollower marks the withheld bearer for an HTTP first URL
