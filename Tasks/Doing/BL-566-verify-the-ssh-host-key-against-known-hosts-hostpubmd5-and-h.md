---
id: BL-566
title: Verify the SSH host key against known_hosts, --hostpubmd5 and --hostpubsha256
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-564]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-566 — Verify the SSH host key against known_hosts, --hostpubmd5 and --hostpubsha256

## Goal

The server's host key is accepted or refused as curl 8.21.0 does: matched against `--hostpubsha256` (base64 SHA-256) or `--hostpubmd5` (hex MD5) when given, else against the known-hosts file (`--knownhosts`, default `~/.ssh/known_hosts`) including hashed `|1|` entries, `[host]:port` entries and `@revoked`/`@cert-authority` markers as curl treats them, with `-k` changing the outcome as curl's does, and every refusal mapped to curl's exit code and message.

## Context

- Conformance audit 2026-09-28, rows 31 and 35. Builds on BL-564 (the host key and its verified signature).
- **BCL only.** `MD5`, `SHA256`, `HMACSHA1` (hashed known_hosts entries). Known-hosts entries of every key type BL-560's ADR offers are matched, `ssh-ed25519` included (its signature check is BL-678), and `@cert-authority` covers `ssh-ed25519-cert-v01@openssh.com` host certificates as curl's libssh2 build treats them. What the BCL lacks is hand-built in `Curl.Cryptography.UnitLibrary` (standing rule, root `CLAUDE.md`, "Decisions", 2026-09-28): never a package, never a task blocked for a missing primitive.
- The known-hosts reader takes text, not a path (tests need no disk); the file is opened through the seam BL-560's ADR names.
- Measure with the reference curl against a local OpenSSH server through `Record-CurlExchange.ps1 -NoServer`: host absent from known_hosts, present with a matching key, present with a different key, a hashed entry, `--hostpubmd5` right and wrong, `--hostpubsha256` right and wrong, and each wrong case with `-k`.

## Acceptance criteria

- [ ] Measured first as above; stderr and exit code of each copied into Notes.
- [ ] `Curl.Protocol.Ssh.UnitTests` pin each measured case against the in-memory peer and known-hosts text.
- [ ] New tests are platform-neutral (no home-directory path assumed; the default path is resolved through the environment seam).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
