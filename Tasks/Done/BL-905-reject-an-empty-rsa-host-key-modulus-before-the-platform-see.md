---
id: BL-905
title: Reject an empty RSA host-key modulus before the platform sees it, so Linux and macOS fail key exchange with -8
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-09-29
completed: 2026-09-29
---
# BL-905 — Reject an empty RSA host-key modulus before the platform sees it, so Linux and macOS fail key exchange with -8

## Goal

`ExchangeKeysAsync_RsaHostKeyThePlatformRefuses_FailsWithMinus8` passes on Windows, Linux and macOS, so the `CI` workflow on `work/dark-factory` goes green and the branch can merge to `master`.

## Context

CI run 36584505880 (commit "chore(tasks): claim BL-664 on dark factory lane 4") failed on ubuntu-latest and macos-latest, passed on windows-latest. The one failing test, in `Curl.Protocol.Ssh.UnitTests/Transport/SshTransportTests.KeyExchange.cs:268`, sends an `ssh-rsa` host key whose exponent and modulus are both empty mpints:

    Expected exception of exact type SshTransferException but caught IndexOutOfRangeException.

`RsaSshSignatureVerifier.Verify` (`Curl.Protocol.Ssh.UnitLibrary/HostKeys/RsaSshSignatureVerifier.cs`) hands the empty modulus straight to `RSA.Create(new RSAParameters { ... })`. On Windows (CNG) that throws `CryptographicException`, which `SshTransport` (line 154) turns into exit -8 with the key-exchange message. On Linux and macOS (OpenSSL) it throws `IndexOutOfRangeException`, which nothing catches.

Fix: in `Verify`, reject an empty modulus before calling `RSA.Create`, throwing `CryptographicException` (the exception `ISshSignatureVerifier` documents for a key the platform refuses), so every platform takes the same path. Keep the check to what the test needs so branch coverage stays at 100%.

## Acceptance criteria

- [x] `RsaSshSignatureVerifier.Verify` throws `CryptographicException` for an `ssh-rsa` host key with an empty modulus, without calling `RSA.Create`.
- [x] `ExchangeKeysAsync_RsaHostKeyThePlatformRefuses_FailsWithMinus8` passes on Windows, and the `CI` workflow passes on ubuntu-latest and macos-latest for the commit that lands this.
- [x] `Curl.Protocol.Ssh.UnitLibrary` keeps 100% line and branch coverage.

## Notes

Blocks the merge of `work/dark-factory` into `master` Stewart asked for on 2026-09-29.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. Empty ssh-rsa modulus now fails key exchange with -8 on every platform; CI run 36588755038 green on Windows, Linux and macOS.
