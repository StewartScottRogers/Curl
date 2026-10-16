---
id: BL-2489
title: SSH: write the known-hosts file when CURLOPT_SSH_KEYFUNCTION answers FINE_ADD_TO_FILE or FINE_REPLACE
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2422]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-10-16
completed:
---
# BL-2489 — SSH: write the known-hosts file when CURLOPT_SSH_KEYFUNCTION answers FINE_ADD_TO_FILE or FINE_REPLACE

## Goal

When `SshOptions.KnownHostDecision` (libcurl's `CURLOPT_SSH_KEYFUNCTION`) answers `AcceptAndAddToFile` or `AcceptAndReplaceInFile` for a host key that did not match, the SSH handler writes the known-hosts file (`SshOptions.KnownHostsPath`) as libcurl 8.22.0's `lib/vssh/libssh2.c` `ssh_knownhost` does: the new `host key` line added, the mismatching entry removed first for `FINE_REPLACE`, and `WARNING: adding the known host <host> failed` / `WARNING: writing <file> failed` info lines when that fails.

## Context

BL-2422 (ADR-0623) made `SshHostKeyChecker` ask `KnownHostDecision` after the known-hosts lookup and accept on all three `FINE*` verdicts, but it writes no file: the in-memory add and `libssh2_knownhost_writefile` are left to this task. Start at `Curl.Protocol.Ssh.UnitLibrary/HostKeys/SshHostKeyChecker.cs` (`CheckKnownHosts`) and `KnownHostsFile` (`LoadAsync` over the handler's `IFileSystem`). Write the file through the handler's `IFileSystem`, never `System.IO.File`, so tests stay off the disk. libssh2's writefile rewrites the whole file in its own line format (plain host names as given, `[host]:port` for a non-22 port); measure what the rewritten file holds with a real curl and libssh2 before pinning it.

## Acceptance criteria

- [ ] `SshHostKeyCheckerTests` (or a new `KnownHostsFileWriterTests`) pin the file written for `AcceptAndAddToFile` on a missing host and for `AcceptAndReplaceInFile` on a mismatch, and that `Accept` writes nothing.
- [ ] A write failure reports libcurl's warning line and still accepts the key.
- [ ] `dotnet build` is clean and the fast tests pass.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 0 failing members.

## Notes

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing.
