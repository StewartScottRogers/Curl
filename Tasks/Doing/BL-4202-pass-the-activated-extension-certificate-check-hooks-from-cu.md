---
id: BL-4202
title: Pass the activated extension certificate check hooks from Curl.Console
priority: High
assignee: Claude
pipeline: direct
depends-on: [BL-4097, BL-4192]
touches: [Curl.Console/CurlComposition.cs, Curl.Console.UnitTests/ExtensionCertificateCheckHookTests.cs, Curl.Extension.Composition.UnitLibrary/ExtensionCertificateCheckHookCatalogue.cs, Tasks/SharedListFiles.txt]
requirement: none
created: 2026-10-29
completed:
---
# BL-4202 — Pass the activated extension certificate check hooks from Curl.Console

## Goal

A curl command line that gives an extension's certificate check option fails with exit 60 when that extension refuses the server's chain, and runs as before when no such option is given.

## Context

BL-4097 added `Curl.Extension.Abstractions.Certificates.CertificateCheckHooks` and the engine's `extensionCertificateConnector` and `extensionTlsProvider` seams on `EngineComposition.CreateProtocolHandlers` (ADR-1016); nothing in the host builds the hooks yet. Do as BL-4192 does for connection hooks: a catalogue in `Curl.Extension.Composition` listing each extension's `ICertificateCheckHook` activator (one line each, registered in `Tasks/SharedListFiles.txt`), and `Curl.Console`'s composition building `CertificateCheckHooks.ActivatedBy` from the option group and passing `ConnectorOver` and `TlsProviderOver`. FTPS data connections (`ftpDataConnector`) are not checked yet (ADR-1016 Consequences).

## Acceptance criteria

- [ ] A Curl.Console unit test with a test extension's certificate check refusing shows the transfer exiting 60 with the check's message, no network.
- [ ] With no extension option given, the host passes curl's connector and TLS provider unchanged (unit test).
- [ ] The changed libraries meet the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 1dfd2cf2 54e224d7 478f867b 3d836d5e.
