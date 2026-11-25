---
id: BL-5971
title: Rename ExtensionProtocolHandlers to ExtendedProtocolHandlers, and move the CURL_EXTENSIONS switch to its own ExtensionSwitch
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Api/Composition/CurlClient.Create.cs, Curl.Conformance.Extended.UnitTests/CurlSurface/Discovery/McpDiscoveryCases.cs, Curl.Console/CurlCommandRunner.cs, Curl.Console/CurlComposition.cs, Curl.Extended.Protocol.Amqp0.UnitTests/Client/Amqp0ExtensionTests.cs, Curl.Extended.Protocol.Amqp1.UnitTests/Client/Amqp1ExtensionTests.cs, Curl.Extended.Protocol.AzureStorage.UnitTests/Client/AzureStorageExtensionTests.cs, Curl.Extended.Protocol.Coap.UnitTests/Client/CoapExtensionTests.cs, Curl.Extended.Protocol.DnsQuery.UnitTests/Client/DnsQueryExtensionTests.cs, Curl.Extended.Protocol.GoogleCloudStorage.UnitTests/Client/GcsExtensionTests.cs, Curl.Extended.Protocol.Grpc.UnitTests/Client/GrpcExtensionTests.cs, Curl.Extended.Protocol.Kafka.UnitTests/Client/KafkaExtensionTests.cs, Curl.Extended.Protocol.McpClient.UnitTests/Client/McpClientExtensionTests.cs, Curl.Extended.Protocol.Mqtt5.UnitTests/Mqtt5ExtensionTests.cs, Curl.Extended.Protocol.Nats.UnitTests/NatsExtensionTests.cs, Curl.Extended.Protocol.OciRegistry.UnitTests/Client/OciRegistryExtensionTests.cs, Curl.Extended.Protocol.Plugin.UnitTests/PluginProtocolExtensionTests.cs, Curl.Extended.Protocol.Redis.UnitTests/Client/RedisExtensionTests.cs, Curl.Extended.Protocol.S3.UnitTests/Client/S3ExtensionTests.cs, Curl.Extended.Protocol.SignalR.UnitTests/SignalRExtensionTests.cs, Curl.Extended.Protocol.SocketIo.UnitTests/SocketIoExtensionTests.cs, Curl.Extended.Protocol.Stomp.UnitTests/Client/StompExtensionTests.cs, Curl.Extended.Protocol.WebTransport.UnitTests/WebTransportOtherTests.cs, Curl.Extended.Protocol.Xmpp.UnitTests/Client/XmppExtensionTests.cs, Curl.Extension.CertificateTransparency.UnitTests/CertificateTransparencyExtensionTests.cs, Curl.Extension.Checksum.UnitTests/ChecksumExtensionTests.cs, Curl.Extension.ClientHints.UnitTests/ClientHintsExtensionTests.cs, Curl.Extension.CodeGeneration.UnitTests/CodeGenerationExtensionTests.cs, Curl.Extension.Composition.UnitLibrary/CLAUDE.md, Curl.Extension.Composition.UnitLibrary/ExtensionCommandLineOptions.cs, Curl.Extension.Composition.UnitLibrary/ExtensionListing.cs, Curl.Extension.Composition.UnitLibrary/ExtensionProtocolHandlers.cs, Curl.Extension.Composition.UnitTests/ExtensionProtocolHandlersTests.cs, Curl.Extension.CompressionDictionary.UnitTests/CompressionDictionaryExtensionTests.cs, Curl.Extension.ConnectRpc.UnitTests/ConnectRpcExtensionTests.cs, Curl.Extension.ContentDigest.UnitTests/ContentDigestExtensionTests.cs, Curl.Extension.Dane.UnitTests/DaneExtensionTests.cs, Curl.Extension.DeltaDownload.UnitTests/DeltaDownloadExtensionTests.cs, Curl.Extension.Dpop.UnitTests/DpopExtensionTests.cs, Curl.Extension.GrpcWeb.UnitTests/GrpcWebExtensionTests.cs, Curl.Extension.Har.UnitTests/HarExtensionTests.cs, Curl.Extension.HttpCache.UnitTests/HttpCacheExtensionTests.cs, Curl.Extension.JsonRpc.UnitTests/JsonRpcExtensionTests.cs, Curl.Extension.MlDsaCertificates.UnitTests/MlDsaCertificatesExtensionTests.cs, Curl.Extension.OAuth2.UnitTests/Options/OAuth2OptionsTests.cs, Curl.Extension.ObliviousHttp.UnitTests/ObliviousHttpExtensionTests.cs, Curl.Extension.OpenTelemetry.UnitTests/OpenTelemetryExtensionTests.cs, Curl.Extension.OsCredentialStore.UnitTests/OsCredentialStoreExtensionTests.cs, Curl.Extension.RateLimit.UnitTests/RateLimitExtensionTests.cs, Curl.Extension.RecordReplay.UnitTests/RecordReplayExtensionTests.cs, Curl.Extension.ResumableUpload.UnitTests/ResumableUploadExtensionTests.cs, Curl.Extension.SegmentedDownload.UnitTests/SegmentedDownloadExtensionTests.cs, Curl.Extension.ServerSentEvents.UnitTests/ServerSentEventsExtensionTests.cs, Curl.Extension.Sigstore.UnitTests/SigstoreExtensionTests.cs, Curl.Extension.StructuredFields.UnitTests/StructuredFieldsExtensionTests.cs, Curl.Extension.WebDavTools.UnitTests/WebDavToolsExtensionTests.cs, Curl.Extension.WebSocketExtendedConnect.UnitTests/Option/WebSocketExtendedConnectExtensionTests.cs, Curl.Extension.WebSocketRepl.UnitTests/WebSocketReplExtensionTests.cs, Curl.Extension.WebSocketSubprotocols.UnitTests/WebSocketSubprotocolsExtensionTests.cs, Curl.Mcp.UnitTests/McpExtensionOptionCheckerTests.cs, Curl.Mcp/CurlMcpHost.cs, Documentation/Planning/Decisions/ADR-1071-curl-api-and-curl-mcp-transfers-reach-the-extensions.md, Documentation/Planning/Decisions/ADR-1192-extension-handlers-read-the-runs-environment-from-protocol-handler-services.md, Documentation/Planning/Decisions/README.md, Documentation/Protocols.md, Documentation/Wiki/Glossary.md, Curl.Extension.Composition.UnitLibrary/ExtendedProtocolHandlers.cs, Curl.Extension.Composition.UnitTests/ExtendedProtocolHandlersTests.cs, Curl.Extension.Composition.UnitLibrary/ExtensionSwitch.cs]
requirement: none
created: 2026-11-25
completed:
---
# BL-5971 — Rename ExtensionProtocolHandlers to ExtendedProtocolHandlers, and move the CURL_EXTENSIONS switch to its own ExtensionSwitch

## Goal

The static class `Curl.Extension.Composition.ExtensionProtocolHandlers` is renamed `ExtendedProtocolHandlers`, matching the glossary's term "extended protocol" (ADR-1177). Its `SwitchVariable` (`CURL_EXTENSIONS`) moves to a new `ExtensionSwitch` class, because that switch turns off every extension, option extensions included, not only the extended protocols' handlers. No behaviour changes.

## Context

Stewart, 2026-11-25: use "extended protocols", never "extension protocols". The prose was fixed in `cbbbcfcaaf` (ADR-1007) and the follow-up wording commit. This class name is the last code use.

Mechanical rename:
- `ExtensionProtocolHandlers.cs` becomes `ExtendedProtocolHandlers.cs`, and `ExtensionProtocolHandlersTests.cs` becomes `ExtendedProtocolHandlersTests.cs`. Test method names that start `ExtensionProtocolHandlers_` (one in each `*ExtensionTests.cs` of the extended protocols) become `ExtendedProtocolHandlers_`.
- `SwitchVariable` goes to `ExtensionSwitch.Variable` in `Curl.Extension.Composition.UnitLibrary/ExtensionSwitch.cs`. Update its callers: `ExtensionCommandLineOptions`, `ExtensionListing`, `CurlCommandRunner`, every option extension's tests (Dane, Checksum and the rest), and `McpExtensionOptionCheckerTests`.
- Update the docs that name the class: the composition project's `CLAUDE.md`, `Documentation/Protocols.md`, the Glossary's Extension row, and ADR-1071, ADR-1192 and `Decisions/README.md`. In accepted ADRs, update only the identifier; record the rename in this task's log.

The touches list is every file that names the class today. Any lane task that adds a new `ExtensionProtocolHandlers` reference while this runs will fail its rebuild after rebase and must switch to the new name. Keep the run short.

## Acceptance criteria

- [ ] `git grep ExtensionProtocolHandlers` finds nothing outside `Tasks/Done`, `Audit` and `Gap`.
- [ ] `CURL_EXTENSIONS` is defined once, as `ExtensionSwitch.Variable`, and `CURL_EXTENSIONS=0` still turns every extension off. The existing tests pin this, renamed but otherwise unchanged.
- [ ] `dotnet build` is clean and the fast tests pass; the composition library stays at 100% line and branch coverage.

## Notes

## Log

- 2026-11-25: Created on Stewart's request ("rename it") after the "extended protocol" wording sweep.
- 2026-11-25: Backlog -> Doing. Criteria at claim: fe612d33 f1b9633a ef56b0bc.
