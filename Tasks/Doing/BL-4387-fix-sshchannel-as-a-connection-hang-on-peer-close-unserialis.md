---
id: BL-4387
title: Fix SshChannel as a connection: hang on peer close, unserialised read and write, close that never ends
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Ssh.UnitLibrary/Connection, Curl.Ssh.UnitTests/Connection]
requirement: none
created: 2026-10-31
completed:
---
# BL-4387 — Fix SshChannel as a connection: hang on peer close, unserialised read and write, close that never ends

## Goal

An SSH channel used as an IConnection (--ssh-jump direct-tcpip) fails fast when the peer closes, never corrupts the packet stream under concurrent read and write, and always finishes closing.

## Context

Shift-end review 2026-10-31 (pr-review-toolkit code-reviewer and type-design-analyzer) of the move to Curl.Ssh.UnitLibrary. Three defects, new because SshChannel now serves curl's HTTP stack through SshDirectTcpipChannel (ADR-1166, ADR-1167):
1. Curl.Ssh.UnitLibrary/Connection/SshChannel.cs:122-125 - SendAsync loops `while (remoteWindow == 0) ReadNextMessageAsync` without checking remoteSentEof/remoteClosed (set near lines 280/283), though its doc comment promises EndOfStreamException. `curl --ssh-jump u@jump -T big http://target/` whose target answers 413 and closes hangs until --max-time.
2. SshChannel.cs:118-136, 233-243, 307-321 - nothing serialises PacketReader, PacketWriter, the windows or the received queue, yet HttpContinueWaitConnection writes the body while a status-line read is pending. Two concurrent reads split the SSH packet stream; a window-adjust write can interleave with a data write and break the MAC sequence.
3. SshDirectTcpipChannel.cs:398-407 calls SshChannel.CloseAsync (146-155) with CancellationToken.None: it sends EOF and waits for the server's CLOSE. OpenSSH sends CLOSE for direct-tcpip only once the target closes, so a target that ignores half-close hangs DisposeAsync forever while received data keeps queueing.

## Acceptance criteria

- [ ] SendAsync throws EndOfStreamException when the peer sent EOF or CLOSE while the remote window is 0, proven by a new test in Curl.Ssh.UnitTests/Connection.
- [ ] Concurrent ReadAsync and WriteAsync on SshDirectTcpipChannel never run two packet reads or two packet writes at once, proven by a test that writes past the remote window while a read is pending.
- [ ] Disposing SshDirectTcpipChannel completes when the server never sends CLOSE (sends EOF and CLOSE together, or bounds the wait), proven by a test.
- [ ] dotnet build is clean and the fast tests of Curl.Ssh.UnitTests and Curl.Extension.SshJump.UnitTests pass; Curl.Ssh.UnitLibrary keeps 100% line and branch coverage.

## Notes

## Log

- 2026-10-31: Created.
- 2026-10-31: Backlog -> Doing. Criteria at claim: e288b318 68e8b809 ffe6974b fb5ba35a.
