---
id: BL-1095
title: Find and fix the Curl.Protocol.Ssh.UnitTests Integration test that hangs on Windows
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-10-01
completed: 2026-10-01
---
# BL-1095 — Find and fix the Curl.Protocol.Ssh.UnitTests Integration test that hangs on Windows

## Goal

`dotnet test Curl.Protocol.Ssh.UnitTests --filter "TestCategory=Integration"` finishes on Windows instead of hanging.

## Context

Found in BL-1035 (2026-10-01, lane 4): on Windows, `--filter "TestCategory=Integration&FullyQualifiedName!~Pageant"` ran past a 120-second timeout (and a full `dotnet test Curl.Protocol.Ssh.UnitTests` past 30 minutes), while the fast tests finish in about 9 seconds and the three new Pageant Integration tests in under a second. The hang was there before BL-1035's changes (none of them are in the hanging set). Suspects: `Authentication/SystemSshAgentConnectorTests`' pipe and Unix socket tests (BL-902), which have no `OSCondition` and no timeout. Run each Integration test alone with `--blame-hang-timeout 60s` to find it.

## Acceptance criteria

- [x] The hanging test is named in Notes with its cause.
- [x] `dotnet test Curl.Protocol.Ssh.UnitTests --filter "TestCategory=Integration"` passes on Windows within 2 minutes.
- [x] A test that only works on one platform carries the matching `OSCondition`.

## Notes

- The hanging test was `SystemSshAgentConnectorTests.ConnectAsync_WindowsPipeServed_ConnectsToIt` (found with `--blame-hang-timeout 60s`, then per-step `WaitAsync` timeouts pinned it to `AssertCarriesBytesAsync`). Cause: the test's `NamedPipeServerStream` is created with the default buffer sizes of 0, so the client's `WriteAsync` completes only once the server reads; the helper awaited the write before starting the read, a deadlock. The connector itself was fine. Fix: start the peer's `ReadExactlyAsync` before awaiting the write.
- The pipe test only exercises the Windows build's pipe path, so it now carries `[OSCondition(OperatingSystems.Windows)]`. The Unix socket test stays cross-platform: Windows 10+ supports AF_UNIX and it passes there.
- After the fix: `dotnet test Curl.Protocol.Ssh.UnitTests --filter "TestCategory=Integration"` passes 6 of 6 in about 4 seconds on Windows.

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. The Ssh Integration tests finish on Windows: the pipe test's byte check no longer deadlocks on an unbuffered pipe
