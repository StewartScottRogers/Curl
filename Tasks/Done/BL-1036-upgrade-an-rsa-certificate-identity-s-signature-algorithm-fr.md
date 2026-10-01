---
id: BL-1036
title: Upgrade an RSA certificate identity's signature algorithm from server-sig-algs as libssh2 does
priority: Low
assignee: Claude
pipeline: protocol
depends-on: [BL-902]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-09-30
completed: 2026-10-01
---
# BL-1036 — Upgrade an RSA certificate identity's signature algorithm from server-sig-algs as libssh2 does

## Goal

An `ssh-rsa-cert-v01@openssh.com` key (from the agent, or `--key` with a certificate) signs with the `rsa-sha2-512-cert-v01@openssh.com` or `rsa-sha2-256-cert-v01@openssh.com` method libssh2 1.11.1 chooses from `server-sig-algs`, instead of its own type.

## Context

`SshUserAuthentication.ChooseSignatureAlgorithm` upgrades only `ssh-rsa` (ADR-0230, ADR-0270). libssh2's `_libssh2_key_sign_algorithm` (`src/userauth.c`) also upgrades the RSA certificate type: `_libssh2_supported_key_sign_algorithms` lists `rsa-sha2-512,rsa-sha2-256,ssh-rsa` for it, the match gets `-cert-v01@openssh.com` appended, and its `SSH_BUG_SIGTYPE` check skips the upgrade for a server banner of OpenSSH 7.7 or older. The agent's sign flags are chosen from the exact method, so a certificate method sends flag 0 (`agent.c` `agent_sign`), and `plain_method` names the plain `rsa-sha2-*` in the signature. Measure with the reference curl and an agent holding an RSA certificate (`ssh-keygen -s`), as ADR-0270 did.

## Acceptance criteria

- [x] Measured first: the `publickey` method, sign flags and signature method curl sends for an RSA certificate identity with `server-sig-algs` naming `rsa-sha2-512`, naming no RSA algorithm, and absent; in Notes.
- [x] `Curl.Protocol.Ssh.UnitTests` pin each measured case against the in-memory peer.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Measured 2026-10-01 (ADR-0310 has the table): a throwaway loopback server from
  `InMemorySshServer` (EXT_INFO added, each `publickey` request's algorithm recorded; not
  committed), an RSA key with an `ssh-keygen -s` certificate passed as `--key k --pubkey
  k-cert.pub`. Ubuntu curl 8.18.0 (libssh2 1.11.1, OpenSSL), through an `nc` relay in WSL:
  `rsa-sha2-512` named -> query `rsa-sha2-512-cert-v01@openssh.com`; `rsa-sha2-256` only ->
  `rsa-sha2-256-cert-v01@openssh.com`; no RSA algorithm -> no request, `No signing signature
  matched`; absent -> `ssh-rsa-cert-v01@openssh.com`; banner `OpenSSH_7.7` ->
  `ssh-rsa-cert-v01@openssh.com`; `OpenSSH_7.8` -> upgraded. Windows curl 8.21.0 (WinCNG):
  `ssh-rsa-cert-v01@openssh.com` with `rsa-sha2-512` named and with no RSA algorithm named -
  WinCNG's `_libssh2_supported_key_sign_algorithms` lists no upgrade for a certificate.
- Sign flags and the signature's method were not measured with an agent (none reachable from
  both builds without a relay); they follow from libssh2's `agent.c` (flag only for the exact
  `rsa-sha2-256`/`rsa-sha2-512`, so 0 for a certificate method) and `plain_method`
  (`rsa-sha2-*`), which the existing ADR-0271 code already applies to the chosen method.
- Decision: a preset with no backend (`Full`) upgrades as the OpenSSL build does. libssh2's
  `SSH_BUG_SIGTYPE` check compares the pointer, not the string (`memcmp(key_method, ...)`),
  which in practice is always unequal, so it reduces to "the method is 28 bytes long" -
  only the RSA certificate among upgradable methods; modelled as the certificate check.
- Key-file certificates still fail at the signature (`SshPrivateKey.KeyType` is never the
  certificate's type); only the method choice is shared with the agent here. Filed as
  BL-1096.

## Log

- 2026-09-30: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. An ssh-rsa-cert-v01@openssh.com identity is upgraded to rsa-sha2-512/256-cert-v01@openssh.com from server-sig-algs as libssh2 1.11.1 does on OpenSSL (not on WinCNG, not for OpenSSH 7.7 or older), measured on both builds (ADR-0310)
