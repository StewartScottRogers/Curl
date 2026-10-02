---
id: BL-1202
title: Write the --trace-config ssh lines for uploads, listings, -Q commands, other logins and failures
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1166]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1202 — Write the --trace-config ssh lines for uploads, listings, -Q commands, other logins and failures

## Goal

Under `-v --trace-config ssh` Curl writes the `[SSH]` lines curl 8.21.0 writes for an SFTP and SCP upload, an SFTP directory listing, `-Q` commands, password, agent and keyboard-interactive logins, `--hostpubsha256`/`--hostpubmd5` checks and failed transfers.

## Context

- BL-1166 traced only a `publickey` login and SFTP and SCP downloads (ADR-0371); `SshStateTrace` is the writer, and the other paths write only the state changes they share with those.
- Measure as BL-1166's Notes describe: `sshd` in WSL Ubuntu (`wsl -u root`; openssh-server is installed) on port 2222 with `KexAlgorithms`, `HostKeyAlgorithms`, `Ciphers` and `MACs` widened for WinCNG, an RSA PEM key in `AuthorizedKeysFile`, and real `curl.exe -v --trace-config ssh --insecure --key ... --pubkey ...`. Leave out the `block=1` and `pollset` lines (ADR-0371).

## Acceptance criteria

- [ ] Each path named in the Goal is measured and its `[SSH]` lines recorded in Notes.
- [ ] `Curl.Protocol.Ssh.UnitTests` pins each against `InMemorySshServer`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-02: Created.
