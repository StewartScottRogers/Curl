---
id: BL-4567
title: Fix AzureStorage conformance: AzureStorage:CredentialPrecedence-2
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.AzureStorage.UnitLibrary/Client/AzureStorageExtension.cs, Curl.Extended.Protocol.AzureStorage.UnitTests/Client/AzureStorageExtensionTests.cs, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4567 — Fix AzureStorage conformance: AzureStorage:CredentialPrecedence-2

## Goal

`az://` reads `AZURE_STORAGE_BEARER_TOKEN` and `AZURE_STORAGE_SAS_TOKEN` from curl's environment, so the credential precedence cases pass.

## Context

Found by BL-4522. `AzureStorageExtension.CreateHandler` passes `Environment.GetEnvironmentVariable`, so the environment the
extended conformance harness gives curl (`ExtendedConformanceHarness.RunAsync(..., environment)`) never reaches the handler:
`AzureBlobCredentialAndFramingCases.Download_BearerVariableAndSasVariable_SendsTheBearerVariable` and
`Download_SasVariableAlone_AppendsTheTokenToTheQueryAndSendsNoAuthorization` send no credential and get `404 ResourceNotFound`
(exit 22). Fix as GCS did (BL-4534): `services.ReadEnvironmentVariable ?? Environment.GetEnvironmentVariable`.

## Acceptance criteria

- [ ] `AzureStorageExtension.CreateHandler` reads tokens through `ProtocolHandlerServices.ReadEnvironmentVariable` when it is set, else the process environment.
- [ ] A focused unit test in `Curl.Extended.Protocol.AzureStorage.UnitTests` pins it (ADR-0482).
- [ ] The `KnownFailures.txt` lines for `AzureStorage:CredentialPrecedence-2`, `AzureStorage:CredentialPrecedence-3` and `AzureStorage:AuthorizeSas-1` are removed and those cases pass.
- [ ] `dotnet build` clean, fast tests green, the library at 100% line and branch coverage.

## Notes

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: c2d91d8c f552bbde d349e2d0 fdc35bd6.
