---
id: BL-4262
title: Forward VerifyServerCertificateChain through CertificateCheckingTlsProvider
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-4249]
touches: [Curl.Extension.Abstractions.UnitLibrary/Certificates/CertificateCheckingTlsProvider.cs, Curl.Extension.Abstractions.UnitTests/Certificates]
requirement: none
created: 2026-10-29
completed:
---
# BL-4262 — Forward VerifyServerCertificateChain through CertificateCheckingTlsProvider

## Goal

When certificate check hooks are active, an extension calling ITlsProvider.VerifyServerCertificateChain on the wrapped provider gets curl's verdict instead of the interface default's exit 60.

## Context

Found by BL-4249 (ADR-1044): CertificateCheckingTlsProvider wraps the engine's provider when hooks are active but does not override the new default method, so it rejects every chain. Forward it to inner (and decide under ADR-1044 whether the hooks also judge the chain).

## Acceptance criteria

- [ ] CertificateCheckingTlsProvider.VerifyServerCertificateChain returns the inner provider's verdict, shown by a unit test.
- [ ] dotnet build is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
