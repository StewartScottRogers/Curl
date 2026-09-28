---
id: BL-681
title: Authenticate an SSH user with an Ed25519 key and read encrypted openssh-key-v1 keys
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-568, BL-672, BL-674, BL-668, BL-737]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-681 — Authenticate an SSH user with an Ed25519 key and read encrypted openssh-key-v1 keys

## Goal

`--key` accepts an Ed25519 private key (`openssh-key-v1`, unencrypted or encrypted) and an encrypted `openssh-key-v1` RSA or ECDSA key decrypted with `--pass` through bcrypt-pbkdf, and the handler authenticates with `ssh-ed25519` public-key signatures, with curl 8.21.0's exit code and message for a wrong passphrase.

## Context

- Conformance audit 2026-09-28, rows 31 and 35; standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28). Builds on BL-568 (public-key user auth and the unencrypted `openssh-key-v1` parser). Primitives: Ed25519 BL-672, Blowfish/bcrypt-pbkdf BL-674; the key's cipher (`aes256-ctr` by default, `aes256-gcm@openssh.com` possible) comes from the BCL.
- OpenSSH `PROTOCOL.key`: `kdfname` `bcrypt`, `kdfoptions` (salt, rounds), the check integers that detect a wrong passphrase.
- Measure with the reference curl against a local OpenSSH server through `Record-CurlExchange.ps1 -NoServer`: an Ed25519 key, an encrypted Ed25519 key with the right and a wrong `--pass`, an encrypted RSA key; stderr and exit code.
- Reference rule: BL-668 (add the `Curl.Cryptography.UnitLibrary` reference and amend `Curl.Protocol.Ssh.UnitLibrary/CLAUDE.md` if no earlier SSH task did).

## Acceptance criteria

- [ ] Measured first as above; stderr and exit code of each copied into Notes.
- [ ] `Curl.Protocol.Ssh.UnitTests` decrypt in-test encrypted keys (generated once with `ssh-keygen -a 16`, committed as test data, never a real user key), pin the Ed25519 signature blob for a fixed session identifier, and pin each measured outcome against the in-memory peer.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
