---
id: BL-1916
title: Emulate SSH user authentication and channels in the case runner
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1899, BL-1951]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests, Curl.Conformance.SshServer.UnitLibrary, Curl.Conformance.SshServer.UnitTests]
requirement: none
created: 2026-10-09
completed:
---
# BL-1916 — Emulate SSH user authentication and channels in the case runner

## Goal

The SSH server stand-in authenticates users (password, public key) and serves session channels with the exec and sftp subsystem requests, giving %USER, %SFTP_PWD and %SCP_PWD their values.

## Context

Harness: Curl.Conformance.UnitLibrary (UpstreamCaseRunner.cs runs a case; UpstreamCaseScreening.cs decides what it skips and why; SwsHttpServerConnector.cs is the in-memory IConnector stand-in for upstream's sws HTTP server). Read Curl.Conformance.UnitLibrary\CLAUDE.md first. The library opens no socket: servers are in-memory IConnector/IDatagramConnector stand-ins, hand-written in the BCL only, platform-neutral, held to 100% line and branch coverage and complexity at most 10. Skip counts are from gap run 2026-10-08_2029. Builds on BL-1899. Implement ssh-userauth (none, password, publickey as the cases need, against the key files the cases name: list the SSH-related variables the cases use and give each a value), connection-layer channel open, data, eof, close and window accounting, exec requests and the subsystem sftp request. SCP and SFTP payloads are BL-1917 and BL-1918. Reference: the upstream curl 8.21.0 release tarball (curl-8_21_0). The cases are vendored in Curl.Conformance.UnitTests\UpstreamTestData (all 2,016 of curl 8.21.0's tests/data files); use them, never a cache under %LOCALAPPDATA%\Curl\gap, which the audit guard refuses a lane. Read tests/server/*.c, tests/*.pl and tests/*.py from the tarball (https://curl.se/download/curl-8.21.0.tar.xz) into an empty scratch folder, never into the repository.

## Acceptance criteria

- [ ] A unit test authenticates by password and by public key against the stand-in and opens a session channel; at least 5 named SSH cases that stop at authentication or a failed login run through UpstreamCaseRunner and get Passed or a real Curl difference.
- [ ] UpstreamCaseScreening no longer returns a skip reason of the form "the harness has no value for %USER, %SFTP_PWD, %SCP_PWD" for those cases (a screening test in Curl.Conformance.UnitTests pins it); any case still skipped for another reason says that reason.
- [ ] Failures the newly measured cases reveal in Curl itself are not fixed here; the next gap run files them. Cases that fail only because of a stand-in fault are fixed in the stand-in.
- [ ] Curl.Conformance.UnitLibrary holds 100% line and branch coverage and complexity of at most 10 per method; tests run on Windows, Linux and macOS with no TestCategory=Integration.
- [ ] `dotnet build Curl.Conformance.UnitLibrary -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
- [ ] Curl.Conformance.UnitLibrary\CLAUDE.md states what the runner now does for this.
- [ ] Interactive check, not a lane gate: `dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "<tests/data>" <folder without blanks>/raw.json <case numbers>` reports the named cases as measured, not skipped.

## Notes

- 2026-10-10 (lane 1): `touches` gains Curl.Conformance.SshServer.UnitLibrary and .UnitTests: the SSH stand-in lives there (ADR-0456), not in Curl.Conformance.UnitLibrary, which may not reference the SSH client's code. No task in Doing on origin/work/dark-factory named either.
- Done so far (uncommitted, shelved by the shift): `SshServerClientAccount` (user `curltest`, password `curltest-password`, a fixed Ed25519 client key written as openssh-key-v1 and an authorized_keys line), `SshServerUserAuthentication` (none fails naming `publickey,password`; publickey PK_OK and Ed25519 signature check; password), `SshServerSessionChannel` (session open, exec/subsystem accepted and other requests refused, data both ways with window accounting, exit-status/EOF/CLOSE), `SshServerConnector.Channels`; in the runner `UpstreamSshServer` (injected, routes port 9003), `%SSHPORT` 9003, `%USER`, `%SFTP_PWD` and `%SCP_PWD` empty (as `%FILE_PWD`, `%LOGDIR` being absolute), `%SSHSRVMD5`/`%SSHSRVSHA256` from the host key, the client key files in `%LOGDIR/server/`; screening lets `sftp`/`scp` run only when the expected exit code is 2, 60 or 67, else skips naming BL-1917/BL-1918; `UpstreamConformanceTests` passes the server and `SshAuthenticationCase_RunThroughCurl_IsMeasuredNotSkipped` (606, 607, 628, 629, 630, 631, 656) shows all seven measured, not skipped. Builds clean; the full conformance run was green (907 passed, 1106 skipped).
- Measured blocker: on Windows all seven fail with exit 2, `Failure establishing ssh session: -5, Unable to exchange encryption keys`: Curl matches libssh2's WinCNG build, which offers no curve25519 or ssh-ed25519, so the stand-in's key exchange cannot agree. That is a stand-in fault, filed as BL-1951 (WinCNG key exchange, RSA host key and RSA client key). With it, the Ed25519 client key here also needs an RSA twin on Windows.
- Still to do after BL-1951: unit tests for the new SshServer classes (100% line and branch coverage), a screening test pinning the %USER/%SFTP_PWD/%SCP_PWD reason gone and the BL-1917/BL-1918 skip reasons, both CLAUDE.md files, and the ratchet list for any case that then passes.
## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
- 2026-10-10: Doing -> Backlog. Waits on BL-1951: on Windows Curl (libssh2 WinCNG) offers no curve25519/ed25519, so the SSH stand-in's key exchange fails with exit 2 before authentication
- 2026-10-10: Backlog -> Doing.
