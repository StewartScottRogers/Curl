---
id: BL-674
title: Hand-build Blowfish and bcrypt-pbkdf
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-670]
touches: [Curl.Cryptography.UnitLibrary, Curl.Cryptography.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-674 — Hand-build Blowfish and bcrypt-pbkdf

## Goal

`Curl.Cryptography.UnitLibrary` encrypts and decrypts Blowfish blocks (with the CBC mode SSH's `blowfish-cbc` needs) and derives keys with OpenBSD's bcrypt-pbkdf, matching published vectors.

## Context

- Consumers: SSH `blowfish-cbc` (libssh2 1.11.1 offers it; BL-680) and encrypted `openssh-key-v1` private keys, whose KDF is `bcrypt` (OpenSSH `PROTOCOL.key`; BL-681). API and rules: BL-669's ADR.
- Blowfish: Schneier's published test vectors (the variable-key and set-key tables). bcrypt-pbkdf: OpenBSD `lib/libutil/bcrypt_pbkdf.c` (bcrypt hash with the "OxychromaticBlowfishSwatDynamite" constant, SHA-512 from the BCL, the output interleaving); vectors from OpenBSD's `regress/lib/libutil/bcrypt_pbkdf` tests, or, failing that, keys generated with `ssh-keygen -a <rounds>` and a known passphrase, their derived key recorded in Notes with how it was obtained.

## Acceptance criteria

- [ ] `Curl.Cryptography.UnitTests` pass the Blowfish ECB vectors, a CBC round trip, and at least three bcrypt-pbkdf vectors (different rounds, salt and output lengths), each with its source cited in a comment.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cryptography.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
