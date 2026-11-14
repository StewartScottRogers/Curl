---
id: BL-5194
title: Fix flaky SSH test ExecuteAsync_SftpListingOpenDirectoryFails_ClosesTheChannelBeforeDisconnecting
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ssh.UnitTests/SshProtocolHandlerTests.FailedSftpTeardown.cs]
requirement: none
created: 2026-11-14
completed:
---
# BL-5194 — Fix flaky SSH test ExecuteAsync_SftpListingOpenDirectoryFails_ClosesTheChannelBeforeDisconnecting

## Goal

ExecuteAsync_SftpListingOpenDirectoryFails_ClosesTheChannelBeforeDisconnecting passes reliably on a loaded CI runner: the SFTP teardown it checks is not cut off by a timing-dependent cancellation.

## Context

Failed once on Windows in CI run 43578396101 (job 132397650607, PR #336 head 8a8b41ced, 2026-11-14 02:11 Arizona) and passed on the rerun of the same commit and in push run 43578396704, so it is flaky, not broken.

Failure: after 8 s, `System.Threading.Tasks.TaskCanceledException: A task was canceled.` thrown from
`SshChannel.WritePacketAsync` (`Curl.Ssh.UnitLibrary/Connection/SshChannel.cs:419`) <- `SendChannelMessageAsync` (:446) <- `SshChannel.CloseAsync` (:212) <- `SftpSessionSlot.CloseIgnoringFailureAsync` (`Curl.Original.Protocol.Ssh.UnitLibrary/Sftp/SftpSessionSlot.cs:94`) <- `SshKeptSession.ShutDownAsync` (:73) <- `SshProtocolHandler.TransferAndKeepOrDisconnectAsync` (:295).

So the channel close write was cancelled during teardown, and the cancellation escaped `CloseIgnoringFailureAsync` (whose name says failures are ignored) and failed the transfer. Decide whether this is a test-timing problem (a stand-in or a timeout too tight for a slow runner; inject `TimeProvider` rather than wall-clock waits) or a product defect (`CloseIgnoringFailureAsync` should also swallow an `OperationCanceledException` from the close), and fix the right one. If it is a product defect, add the product file to `touches` before editing it.

## Acceptance criteria

- [ ] The cause is named in the Log: test timing, or a cancellation escaping `SftpSessionSlot.CloseIgnoringFailureAsync`.
- [ ] The test no longer depends on wall-clock timing (no real delay or timeout decides its outcome), or the product no longer lets a teardown cancellation fail the transfer, with a focused unit test for that.
- [ ] `dotnet test Curl.Original.Protocol.Ssh.UnitTests --filter "FullyQualifiedName~ExecuteAsync_SftpListingOpenDirectoryFails_ClosesTheChannelBeforeDisconnecting"` passes 20 runs in a row.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing. Criteria at claim: 28dc01b7 c93bfb91 cce0abcc a0b49a29.
