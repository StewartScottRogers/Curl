---
id: BL-888
title: Measure the OpenSSL curl build's SSH group-exchange request sizes and split them by preset if they differ
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-564]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-888 — Measure the OpenSSL curl build's SSH group-exchange request sizes and split them by preset if they differ

## Goal

`diffie-hellman-group-exchange-*` asks for, and accepts, the group sizes the platform's curl asks for: the Windows build's measured (2048, 4096, 4096), and the OpenSSL build's as measured.

## Context

- ADR-0206 (BL-564) pinned `GroupExchangeSshKeyExchange.MinimumBits`/`PreferredBits`/`MaximumBits` at (2048, 4096, 4096) on every preset, measured from the Windows reference build (`curl 8.21.0 ... libssh2/1.11.1`, WinCNG). The OpenSSL reference build (`curlimages/curl:8.21.0`) could not be measured that day because the Docker engine was down.
- Measure: run the container's curl with `-s -S -k -u u:p -m 10 sftp://host.docker.internal:<port>/x` against a listener that sends `SSH-2.0-OpenSSH_9.7\r\n` and a `KEXINIT` offering only `diffie-hellman-group-exchange-sha256` (the builder BL-564 used is described in its Notes), and decode the three `uint32`s of the client's `SSH_MSG_KEX_DH_GEX_REQUEST` (message 34) that follows its `KEXINIT`.
- If they differ, move the sizes onto `SshAlgorithmPreferences` (the Windows preset keeps (2048, 4096, 4096)) and amend ADR-0206 with a new ADR.

## Acceptance criteria

- [ ] The OpenSSL build's (minimum, preferred, maximum) is recorded in Notes with the command that measured it.
- [ ] A `Curl.Protocol.Ssh.UnitTests` test pins the request each preset sends, and a group-exchange exchange passes for a prime at the preset's maximum.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
