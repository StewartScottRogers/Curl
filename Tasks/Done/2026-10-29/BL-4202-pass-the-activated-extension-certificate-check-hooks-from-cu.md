---
id: BL-4202
title: Pass the activated extension certificate check hooks from Curl.Console
priority: High
assignee: Claude
pipeline: direct
depends-on: [BL-4097, BL-4192]
touches: [Curl.Console/CurlComposition.cs, Curl.Console.UnitTests/ExtensionCertificateCheckHookTests.cs, Curl.Extension.Composition.UnitLibrary/ExtensionCertificateCheckHookCatalogue.cs, Curl.Extension.Composition.UnitLibrary/CLAUDE.md, .gitattributes, Tasks/SharedListFiles.txt]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4202 — Pass the activated extension certificate check hooks from Curl.Console

## Goal

A curl command line that gives an extension's certificate check option fails with exit 60 when that extension refuses the server's chain, and runs as before when no such option is given.

## Context

BL-4097 added `Curl.Extension.Abstractions.Certificates.CertificateCheckHooks` and the engine's `extensionCertificateConnector` and `extensionTlsProvider` seams on `EngineComposition.CreateProtocolHandlers` (ADR-1016); nothing in the host builds the hooks yet. Do as BL-4192 does for connection hooks: a catalogue in `Curl.Extension.Composition` listing each extension's `ICertificateCheckHook` activator (one line each, registered in `Tasks/SharedListFiles.txt`), and `Curl.Console`'s composition building `CertificateCheckHooks.ActivatedBy` from the option group and passing `ConnectorOver` and `TlsProviderOver`. FTPS data connections (`ftpDataConnector`) are not checked yet (ADR-1016 Consequences).

## Acceptance criteria

- [x] A Curl.Console unit test with a test extension's certificate check refusing shows the transfer exiting 60 with the check's message, no network.
- [x] With no extension option given, the host passes curl's connector and TLS provider unchanged (unit test).
- [x] The changed libraries meet the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Added `.gitattributes` (the catalogue's `merge=union` line, as BL-4192 and BL-4200 did) and `Curl.Extension.Composition.UnitLibrary/CLAUDE.md` (its catalogue paragraph) to touches; no task in Doing on `origin/work/dark-factory` names either.
- `ExtensionCertificateCheckHookCatalogue` (empty, one activator per line), `CurlComposition.ExtensionCertificateCheckHooksOf` and `ExtensionCertificateConnectorOf`/`ExtensionTlsProviderOf` (null without a check). All three compositions (network, dialing, test `CreateRunner`) pass both seams. Unlike connection and resolver hooks, several certificate checks are no conflict: every activated one runs (ADR-1016), so there is no exit-2 path.
- A refusal prints curl's whole exit-60 text, the `More details here: https://curl.se/docs/sslcerts.html` trailer included, as any (60) does; the test pins the first two lines.
- Measure-CodeQuality.ps1 -Library Curl.Console,Curl.Extension.Composition.UnitLibrary: 0 failing members. Fast tests all green (Curl.Console.UnitTests with the 5 new). The tests use no path, OS text or real certificate, so they are platform-neutral.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 1dfd2cf2 54e224d7 478f867b 3d836d5e.
- 2026-10-29: Doing -> Done. An extension's certificate check option now fails a transfer with exit 60 when its check refuses the chain; without one curl's connector and TLS provider pass unchanged
