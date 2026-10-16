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
completed: 2026-10-16
---
# BL-2489 — SSH: write the known-hosts file when CURLOPT_SSH_KEYFUNCTION answers FINE_ADD_TO_FILE or FINE_REPLACE

## Goal

When `SshOptions.KnownHostDecision` (libcurl's `CURLOPT_SSH_KEYFUNCTION`) answers `AcceptAndAddToFile` or `AcceptAndReplaceInFile` for a host key that did not match, the SSH handler writes the known-hosts file (`SshOptions.KnownHostsPath`) as libcurl 8.22.0's `lib/vssh/libssh2.c` `ssh_knownhost` does: the new `host key` line added, the mismatching entry removed first for `FINE_REPLACE`, and `WARNING: adding the known host <host> failed` / `WARNING: writing <file> failed` info lines when that fails.

## Context

BL-2422 (ADR-0623) made `SshHostKeyChecker` ask `KnownHostDecision` after the known-hosts lookup and accept on all three `FINE*` verdicts, but it writes no file: the in-memory add and `libssh2_knownhost_writefile` are left to this task. Start at `Curl.Protocol.Ssh.UnitLibrary/HostKeys/SshHostKeyChecker.cs` (`CheckKnownHosts`) and `KnownHostsFile` (`LoadAsync` over the handler's `IFileSystem`). Write the file through the handler's `IFileSystem`, never `System.IO.File`, so tests stay off the disk. libssh2's writefile rewrites the whole file in its own line format (plain host names as given, `[host]:port` for a non-22 port); measure what the rewritten file holds with a real curl and libssh2 before pinning it.

## Acceptance criteria

- [x] `SshHostKeyCheckerTests` (or a new `KnownHostsFileWriterTests`) pin the file written for `AcceptAndAddToFile` on a missing host and for `AcceptAndReplaceInFile` on a mismatch, and that `Accept` writes nothing.
- [x] A write failure reports libcurl's warning line and still accepts the key.
- [x] `dotnet build` is clean and the fast tests pass.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 0 failing members.

## Notes

- Decided in ADR-0625 (Claude under Stewart's delegation): `SshHostKeyChecker.Check` returns the known-hosts file to write back, or `null`. `KnownHostsFile.WithHostKey` adds the URL's plain host name (curl's `conn->host.name`, no port) last and, for `AcceptAndReplaceInFile`, drops the lookup's entry (`KnownHostsLookup.Entry`) first. `SshProtocolHandler` writes it in `SSH_HOSTKEY` through `IFileSystem` (truncate, `0666`, UTF-8 without BOM, `Environment.NewLine` as libssh2's text-mode `fopen`); a refused open or an `IOException` reports `WARNING: writing <path> failed` and the key stays accepted.
- Not measured against real curl: the curl tool never sets `CURLOPT_SSH_KEYFUNCTION` (its default callback answers only FINE or REJECT), so no command line produces a write. The format follows libssh2 1.11.1's writefile and curl 8.22.0's `ssh_knownhost`. `WARNING: adding the known host` is unreachable (`libssh2_knownhost_add` fails only on out-of-memory), so it is not produced.
- `KnownHostsEntry` now keeps `TypeName` and `Comment` so lines round-trip; one entry per name, so a comma-separated line comes back one line per name, as libssh2 writes it.
- Tests added: `KnownHostsFileTests.Write`, `SshHostKeyCheckerTests.KnownHostsWrite`, `SshProtocolHandlerTests.KnownHostsWrite`, with the fake `WritableInMemoryFileSystem`. Full fast run green; Measure-CodeQuality for the SSH library: 0 failing members.

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing.
- 2026-10-16: Doing -> Done. CURLOPT_SSH_KEYFUNCTION FINE_ADD_TO_FILE and FINE_REPLACE now write the known-hosts file as libssh2 does, warning when the write fails
