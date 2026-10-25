---
id: BL-3511
title: Make Curl.Protocol.Ssh.UnitTests' key-decryption tests fast under coverage
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Ssh.UnitTests/Fakes/TestUserKeys.cs]
requirement: none
created: 2026-10-25
completed:
---
# BL-3511 — Make Curl.Protocol.Ssh.UnitTests' key-decryption tests fast under coverage

## Goal

`Curl.Protocol.Ssh.UnitTests` runs in a fraction of its current time under coverage, because its encrypted `openssh-key-v1` test keys use the fewest bcrypt-pbkdf rounds that still run every code path, so CI's Quality gates shard 1 stops setting a `CI` run's wall time on slow runners (BL-3464).

## Context

BL-3464 sharded the Quality gates job; `Curl.Protocol.Ssh.UnitTests` sits alone in shard 1 and took 2m10s to 12m22s on CI (runs 40205671348 and 40204269817), the long pole of run 40204269817. Under coverage about 22 tests take 17 to 48 seconds each, all of them decrypting the encrypted `openssh-key-v1` fixtures in `Curl.Protocol.Ssh.UnitTests/Fakes/TestUserKeys.cs` (`RsaOpenSshEncrypted`, the ten `Ed25519OpenSshEncrypted` keys, `EcdsaP256OpenSshAes256Gcm`). Those were written by `ssh-keygen -a 16`: 16 bcrypt-pbkdf rounds, each 129 Blowfish key expansions whose F function reads all four S-boxes in full to stay constant-time (ADR-0400, `Curl.Cryptography.UnitLibrary/BlowfishState.cs`), and the coverage collector instruments that loop. The round count is a property of the fixture, not of the code under test: re-encrypting the same keys with `ssh-keygen -p -a 2` keeps every cipher, the multi-round path of `BcryptPbkdf.DeriveBlock` and every assertion, and costs an eighth as much. Production code does not change.

## Acceptance criteria

- [ ] Every encrypted `openssh-key-v1` fixture in `Curl.Protocol.Ssh.UnitTests/Fakes/TestUserKeys.cs` carries 2 bcrypt rounds, is the same key (same public key and comment) with the same cipher and passphrase, and its doc comment names the `ssh-keygen` command and OpenSSH version that wrote it.
- [ ] No test in `Curl.Protocol.Ssh.UnitTests` is removed, re-categorised or has an assertion changed; `git diff` touches only `TestUserKeys.cs` outside `Tasks/`.
- [ ] `dotnet test Curl.Protocol.Ssh.UnitTests --collect:"Code Coverage;Format=cobertura" --filter "TestCategory!=Integration"` passes, and its local wall time before and after is recorded in Notes.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports no member outside a gate.
- [ ] `dotnet build` is clean and the fast tests of `Curl.Protocol.Ssh.UnitTests` pass.
- [ ] The CI run for the push records shard 1's time and the run's wall time in Notes here and in BL-3464's Notes.

## Notes

## Log

- 2026-10-25: Created.
- 2026-10-25: Backlog -> Doing. Criteria at claim: 06cf1dc8 21232fd4 6ec49597 04eff97c e332083b ec4121aa.
