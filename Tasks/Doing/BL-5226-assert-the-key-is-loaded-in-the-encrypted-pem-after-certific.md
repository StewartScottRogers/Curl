---
id: BL-5226
title: Assert the key is loaded in the encrypted-PEM-after-certificate test (AF-0765)
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitTests/HandBuiltPrivateKeyReaderTests.cs]
requirement: none
created: 2026-11-15
completed:
---
# BL-5226 — Assert the key is loaded in the encrypted-PEM-after-certificate test (AF-0765)

## Goal

`LoadAsOpenSslBuild_WithAnEncryptedPemKeyAfterACertificateBlock_SkipsTheCertificateAndDecryptsTheKey` proves that the key was decrypted and loaded, as its name says, and does not stop at proving that no failure came back.

## Context

The shift-end review of 2026-11-15 (ADR-0710) found this with the silent-failure-hunter agent. It reviewed the shift merged at d04b275ae7.

- The test in `Curl.Networking.UnitTests/HandBuiltPrivateKeyReaderTests.cs` (around line 296) was added for AF-0765.
- Its only assertion is `Assert.IsNull(failure)`, followed by `loaded?.Dispose()`. `ActLoad` writes diagnostics only and asserts nothing.
- A loader that returned `(null, null)` would pass. So would a loader that returned a certificate without the decrypted signing key. The name claims "DecryptsTheKey", which breaks CLAUDE.md's "say what it does" rule.
- The neighbouring tests show the pattern to follow. `LoadAsOpenSslBuild_WithAPemKeyAfterAnotherBlock_FindsThePrivateKey` asserts the `HandBuiltKeyCertificate` signing key type inside `using (loaded)`.

## Acceptance criteria

- [ ] The test asserts that `loaded` is a `HandBuiltKeyCertificate` whose `SigningKey` is the Ed25519 signing key, or that the key can sign with `TlsSignatureScheme.Ed25519`.
- [ ] `loaded` is disposed through `using`, not `loaded?.Dispose()`, so a failed assertion does not leak it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing. Criteria at claim: 228843cb 42653e78 9742157d.
