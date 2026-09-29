---
id: BL-972
title: Refuse a zero RSA coefficient in RsaSshPrivateKey before OpenSSL does, so Linux and macOS throw CryptographicException exactly
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ssh.UnitLibrary/Keys/RsaSshPrivateKey.cs]
requirement: none
created: 2026-09-29
completed:
---
# BL-972 — Refuse a zero RSA coefficient in RsaSshPrivateKey before OpenSSL does, so Linux and macOS throw CryptographicException exactly

## Goal

`RsaSshPrivateKeyTests.FromComponents_ZeroCoefficient_Throws` passes on Windows, Linux and macOS, so the `CI` workflow on `work/dark-factory` is green again.

## Context

Since BL-568 (commit 0729839c) the CI runs fail on ubuntu-latest and macos-latest, for example run 36637529664:

    Expected exception of exact type CryptographicException but caught OpenSslCryptographicException.

`RsaSshPrivateKey.FromComponents` hands a zero coefficient to the platform's RSA import. Windows throws `CryptographicException`; OpenSSL throws its subclass `OpenSslCryptographicException`. The same shape as BL-905: refuse the value in our code before the platform sees it, so every platform throws the same type.

## Acceptance criteria

- [ ] `RsaSshPrivateKey.FromComponents` throws `CryptographicException` itself for a zero coefficient, beside its existing prime check.
- [ ] `FromComponents_ZeroCoefficient_Throws` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands this.

## Notes

Claimed while lane 1 held BL-572 in the same project; BL-572 changes only `Sftp/`, `SshProtocolHandler.cs` and `SshTransferException.cs`, not `Keys/`, so the two cannot conflict.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
