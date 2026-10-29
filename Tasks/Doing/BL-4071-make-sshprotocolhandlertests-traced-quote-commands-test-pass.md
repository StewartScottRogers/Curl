---
id: BL-4071
title: Make SshProtocolHandlerTests' traced quote commands test pass every run
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Ssh.UnitTests/SshProtocolHandlerTests.StateTraceOtherPaths.cs]
requirement: none
created: 2026-10-29
completed:
---
# BL-4071 — Make SshProtocolHandlerTests' traced quote commands test pass every run

## Goal

`SshProtocolHandlerTests.ExecuteAsync_TracedQuoteCommandsBeforeAndAfter_WritesTheQuoteStates` passes on every run of the fast tests, not only when the machine is idle.

## Context

Found by BL-4070's lane on 2026-10-29: the test failed once (after 6 s) in a full `dotnet test --filter "TestCategory!=Integration"` run while other lanes were building, and passed in the next full run with nothing changed in Curl.Protocol.Ssh. Find what it waits on with a time limit or a race (look for a fixed timeout, or a read of the trace before the transfer has written it) and make it wait for the event it needs.

## Acceptance criteria

- [ ] The test waits for the event it checks rather than a time limit a busy runner can pass.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: f4d39644 779790c0.
