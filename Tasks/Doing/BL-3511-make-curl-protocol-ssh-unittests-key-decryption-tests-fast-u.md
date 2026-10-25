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

- [x] Every encrypted `openssh-key-v1` fixture in `Curl.Protocol.Ssh.UnitTests/Fakes/TestUserKeys.cs` carries 2 bcrypt rounds, is the same key (same public key and comment) with the same cipher and passphrase, and its doc comment names the `ssh-keygen` command and OpenSSH version that wrote it.
- [x] No test in `Curl.Protocol.Ssh.UnitTests` is removed, re-categorised or has an assertion changed; `git diff` touches only `TestUserKeys.cs` outside `Tasks/`.
- [x] `dotnet test Curl.Protocol.Ssh.UnitTests --collect:"Code Coverage;Format=cobertura" --filter "TestCategory!=Integration"` passes, and its local wall time before and after is recorded in Notes.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports no member outside a gate.
- [x] `dotnet build` is clean and the fast tests of `Curl.Protocol.Ssh.UnitTests` pass.
- [ ] The CI run for the push records shard 1's time and the run's wall time in Notes here and in BL-3464's Notes.

## Notes

- Which job set the wall time (first job start to last job end; the 23, 18 and 25 minutes quoted for these runs are created-to-updated and include queueing): run 40204269817 14m37s, shard 1 (SSH) 12m22s, the long pole; run 40205671348 12m32s, shard 1 only 2m10s, shards 2 and 3 about 7m30s, and the scoring job waited 3 minutes for a runner; run 40206727905 18m38s, shard 1 5m25s, the long pole was runner contention: its jobs started anywhere from 15:29 to 15:40 UTC while the shift-end merge's CI on `factory/merge-20261025-081939`, master's CI, Release and Integration tests ran at once. The 5 to 8.5 minutes before any job started in all three are ci.yml's concurrency group (`cancel-in-progress` is false on `work/dark-factory`, so a push waits for the run before it). So the SSH shard was the long pole in one run of three; runner contention and the concurrency wait were the others.
- Cause: the encrypted `openssh-key-v1` fixtures were written with `ssh-keygen -a 16`. Each bcrypt round is 129 Blowfish key expansions, and Curl.Cryptography's constant-time F function (ADR-0400) reads all four S-boxes in full on every call; the coverage collector instruments that loop, so one decryption cost 17 to 48 seconds under coverage (the whole project runs in 3 s without coverage).
- Change: re-encrypted the same 12 keys with `ssh-keygen -q -p -a 2 -Z <cipher> -P <pass> -N <pass> -f <file>` (OpenSSH_10.5p1, Git for Windows), driven by a scratch PowerShell script that pulled each PEM out of TestUserKeys.cs, re-encrypted a copy, and checked that `ssh-keygen -y` gave the same public key before and after. The PEMs in the file were then compared byte for byte with ssh-keygen's output. 2 rounds, not 1, so `BcryptPbkdf.DeriveBlock`'s multi-round loop still runs. Same ciphers, passphrases, public keys and comments; no test or assertion changed; production code unchanged.
- Local (32 logical processors), `dotnet test Curl.Protocol.Ssh.UnitTests --collect:"Code Coverage;Format=cobertura" --filter "TestCategory!=Integration"`: before, 64 s wall (test run 59 s, summed test time 1,336 s, slowest test 47.8 s); after, 27 s wall (test run 23 s, summed 706 s, slowest 7.8 s). 1899 tests pass both times.
- `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary`: 0 failing members. `dotnet build`: 0 warnings, 0 errors.

## Log

- 2026-10-25: Created.
- 2026-10-25: Backlog -> Doing. Criteria at claim: 06cf1dc8 21232fd4 6ec49597 04eff97c e332083b ec4121aa.
