---
id: BL-1086
title: Find and fix the Curl.Protocol.Ssh.UnitTests Integration test that hangs on Windows
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-10-01
completed:
---
# BL-1086 — Find and fix the Curl.Protocol.Ssh.UnitTests Integration test that hangs on Windows

## Goal

`dotnet test Curl.Protocol.Ssh.UnitTests --filter "TestCategory=Integration"` finishes on Windows instead of hanging.

## Context

Found in BL-1035 (2026-10-01, lane 4): on Windows, `--filter "TestCategory=Integration&FullyQualifiedName!~Pageant"` ran past a 120-second timeout (and a full `dotnet test Curl.Protocol.Ssh.UnitTests` past 30 minutes), while the fast tests finish in about 9 seconds and the three new Pageant Integration tests in under a second. The hang was there before BL-1035's changes (none of them are in the hanging set). Suspects: `Authentication/SystemSshAgentConnectorTests`' pipe and Unix socket tests (BL-902), which have no `OSCondition` and no timeout. Run each Integration test alone with `--blame-hang-timeout 60s` to find it.

## Acceptance criteria

- [ ] The hanging test is named in Notes with its cause.
- [ ] `dotnet test Curl.Protocol.Ssh.UnitTests --filter "TestCategory=Integration"` passes on Windows within 2 minutes.
- [ ] A test that only works on one platform carries the matching `OSCondition`.

## Notes

## Log

- 2026-10-01: Created.
