---
id: BL-1953
title: Commit the SSH stand-in's user authentication and session channels with full unit-test coverage
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1899, BL-1951]
touches: [Curl.Conformance.SshServer.UnitLibrary, Curl.Conformance.SshServer.UnitTests]
requirement: none
created: 2026-10-10
completed:
---
# BL-1953 — Commit the SSH stand-in's user authentication and session channels with full unit-test coverage

## Goal

The SSH stand-in's user authentication (none, password, Ed25519 and RSA public key) and session channels (open, exec and subsystem requests, data with window accounting, exit-status, EOF, close) are committed in Curl.Conformance.SshServer.UnitLibrary at 100% line and branch coverage.

## Context

Split from BL-1916, which the factory's per-task cost cap stopped. The code is already written: it is in stash 51b9f0bb6 ("darkfactory BL-1916 20261009-213949"). Apply it by hash (`git stash apply 51b9f0bb6`), never pop; keep only its Curl.Conformance.SshServer.* changes here (the runner and screening changes are BL-1954). The classes: SshServerClientAccount (user `curltest`, password `curltest-password`, a fixed Ed25519 client key as openssh-key-v1 and an authorized_keys line), SshServerUserAuthentication, SshServerSessionChannel and SshServerConnector.Channels. BL-1951 added the WinCNG key exchange and RSA host key; give the client account an RSA key twin too, since Curl on Windows (libssh2 WinCNG) offers no ssh-ed25519. Read BL-1916's Notes and ADR-0456 first.

## Acceptance criteria

- [ ] Unit tests in Curl.Conformance.SshServer.UnitTests authenticate by password, by Ed25519 public key and by RSA public key, refuse `none` naming `publickey,password`, open a session channel, accept exec and subsystem requests and refuse others, move data both ways within the window, and send exit-status, EOF and CLOSE.
- [ ] Curl.Conformance.SshServer.UnitLibrary holds 100% line and branch coverage and complexity of at most 10 per method (`powershell -NoProfile -File Measure-CodeQuality.ps1`); tests are platform-neutral with no TestCategory=Integration.
- [ ] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
- [ ] Curl.Conformance.SshServer.UnitLibrary\CLAUDE.md states what the stand-in now does for authentication and channels.

## Notes

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
