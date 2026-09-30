---
id: BL-1034
title: Upgrade an RSA certificate identity's signature algorithm from server-sig-algs as libssh2 does
priority: Low
assignee: Claude
pipeline: protocol
depends-on: [BL-902]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-09-30
completed:
---
# BL-1034 — Upgrade an RSA certificate identity's signature algorithm from server-sig-algs as libssh2 does

## Goal

An `ssh-rsa-cert-v01@openssh.com` key (from the agent, or `--key` with a certificate) signs with the `rsa-sha2-512-cert-v01@openssh.com` or `rsa-sha2-256-cert-v01@openssh.com` method libssh2 1.11.1 chooses from `server-sig-algs`, instead of its own type.

## Context

`SshUserAuthentication.ChooseSignatureAlgorithm` upgrades only `ssh-rsa` (ADR-0230, ADR-0270). libssh2's `_libssh2_key_sign_algorithm` (`src/userauth.c`) also upgrades the RSA certificate type: `_libssh2_supported_key_sign_algorithms` lists `rsa-sha2-512,rsa-sha2-256,ssh-rsa` for it, the match gets `-cert-v01@openssh.com` appended, and its `SSH_BUG_SIGTYPE` check skips the upgrade for a server banner of OpenSSH 7.7 or older. The agent's sign flags are chosen from the exact method, so a certificate method sends flag 0 (`agent.c` `agent_sign`), and `plain_method` names the plain `rsa-sha2-*` in the signature. Measure with the reference curl and an agent holding an RSA certificate (`ssh-keygen -s`), as ADR-0270 did.

## Acceptance criteria

- [ ] Measured first: the `publickey` method, sign flags and signature method curl sends for an RSA certificate identity with `server-sig-algs` naming `rsa-sha2-512`, naming no RSA algorithm, and absent; in Notes.
- [ ] `Curl.Protocol.Ssh.UnitTests` pin each measured case against the in-memory peer.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-30: Created.
