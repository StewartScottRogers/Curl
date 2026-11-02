---
id: BL-4540
title: Fix AzureStorage: close-delimited body over TLS without close_notify escapes as MissingCloseNotifyException
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.AzureStorage.UnitLibrary/Http, Curl.Extended.Protocol.AzureStorage.UnitTests/Http, Curl.Extended.Protocol.AzureStorage.UnitLibrary/Client/AzureStorageProtocolHandler.cs, Curl.Extended.Protocol.AzureStorage.UnitTests/Client/AzureStorageProtocolHandlerTests.cs, Curl.Extended.Protocol.AzureStorage.UnitTests/Client/QueuedAzureStorageConnector.cs]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4540 — Fix AzureStorage: close-delimited body over TLS without close_notify escapes as MissingCloseNotifyException

## Goal

An `az://` download whose response is close-delimited (`Connection: close`, no length) over TLS ends with an exit code, never an exception escaping the handler.

## Context

Found by BL-4520: `AzureBlobReferenceEndpoint` with `AzureStorageResponseFraming.Close` behind `ReferenceRoute.Tls` makes
`InProcessCurl.RunAsync` throw `Curl.Protocol.Abstractions.MissingCloseNotifyException` ("schannel: server closed abruptly
(missing close_notify)") from `AzureStorageHttpConnectionReader.FillAsync`, because the reference TLS stream closes without
close_notify. Decide (ADR) what the client does then - measure how curl's HTTP layer treats a close-delimited body without
close_notify (exit 56 with that message, or the body accepted) and match it.

## Acceptance criteria

- [x] `MissingCloseNotifyException` while reading an Azure Storage response body becomes a `TransferResult` failure with curl's exit code, decided by an ADR.
- [x] A focused unit test in `Curl.Extended.Protocol.AzureStorage.UnitTests` pins it (ADR-0482).
- [x] `dotnet build` clean, fast tests green, the library at 100% line and branch coverage.

## Notes

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: a77d74ca f552bbde fdc35bd6.
- 2026-11-02: Doing -> Done. A TLS read without close_notify in an az:// or abfs:// transfer now ends with exit 56 and the TLS build's text (ADR-1194)
