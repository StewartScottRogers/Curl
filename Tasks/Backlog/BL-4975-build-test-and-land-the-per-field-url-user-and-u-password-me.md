---
id: BL-4975
title: Build, test and land the per-field URL user and -u password merge
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/TransferCredentialLookup.cs, Curl.Engine.UnitLibrary/CLAUDE.md, Curl.Engine.UnitLibrary/TransferContextFactory.cs, Curl.Engine.UnitTests/UrlUserWithOptionPasswordTests.cs]
requirement: none
created: 2026-11-11
completed:
---
# BL-4975 — Build, test and land the per-field URL user and -u password merge

## Goal

The merge of a URL user name with the -u password (BL-4915) builds, passes the fast tests and is committed.

## Context

Split from BL-4915. The fix is already written in `TransferCredentialLookup` (`OptionPasswordOf`) with tests in `Curl.Engine.UnitTests/UrlUserWithOptionPasswordTests.cs` and an updated `Curl.Engine.UnitLibrary/CLAUDE.md`; it was never built. BL-4915's Notes say what changed.

## Acceptance criteria

- [ ] `dotnet build` is clean.
- [ ] `dotnet test --filter "TestCategory!=Integration"` passes, including UrlUserWithOptionPasswordTests.
- [ ] The change is committed.

## Notes

Split from BL-4915 (over its cost cap).

Continues the stashed work of BL-4915.

## Log

- 2026-11-11: Created.
