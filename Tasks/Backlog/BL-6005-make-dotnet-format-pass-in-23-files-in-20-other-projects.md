---
id: BL-6005
title: Make dotnet format pass in 23 files in 20 other projects
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Api.UnitTests/EngineContracts/Multi/MultiValueTypeTests.cs, Curl.Api.UnitTests/Readme/ReadmeSamples.cs, Curl.Api/Client/CurlClient.cs, Curl.Cli.UnitLibrary/ConfigFileSyntax.cs, Curl.Cryptography.UnitLibrary/MontgomeryModulus.cs, Curl.Extended.Protocol.Amqp0.UnitTests/Client/Amqp0ClientFlowTests.cs, Curl.Extended.Protocol.AzureStorage.UnitLibrary/Requests/AzureStorageCredential.cs, Curl.Extended.Protocol.Coap.UnitLibrary/Client/CoapExtension.cs, Curl.Extended.Protocol.Coap.UnitLibrary/Observe/CoapObserveTransferHook.cs, Curl.Extended.Protocol.McpClient.UnitLibrary/Client/McpClientExtension.cs, Curl.Extended.Protocol.Plugin.UnitTests/BatchHelperArgumentTests.cs, Curl.Extended.Protocol.SignalR.UnitLibrary/SignalRExtension.cs, Curl.Extended.Protocol.Stomp.UnitTests/Wire/StompFrameReaderTests.cs, Curl.Extended.Protocol.WebTransport.UnitTests/WebTransportConnectHeaderFieldsTests.cs, Curl.Extension.HttpCache.UnitTests/CacheDirectoryTests.cs, Curl.Extension.PrivacyPass.UnitTests/Tokens/TestIssuer.cs, Curl.Extension.WireGuard.UnitLibrary/Tunnel/WireGuardConnector.cs, Curl.Mcp.UnitLibrary/McpArgumentChecker.cs, Curl.Output.UnitTests/Cgr214Tests.cs, Curl.Protocol.Abstractions.UnitLibrary/TlsHandshakeEvent.cs, Libcurl.UnitTests/Cgr247Tests.cs, Libcurl.UnitTests/Misc/ErrorText/CurlErrorTextTests.cs, Libcurl/Multi/Settings/CurlMultiOption.cs]
requirement: none
created: 2026-11-26
completed:
---
# BL-6005 — Make dotnet format pass in 23 files in 20 other projects

## Goal

`dotnet format Curl.slnx --verify-no-changes --include <each path in touches>` exits 0: the `using` order, whitespace and final newlines in this task's touches follow `.editorconfig`.

## Context

Part of BL-5923, split by project group so lanes run the parts in parallel. On 2026-11-26 `dotnet format` reported 680 files across 53 projects: 636 IMPORTS (`using` order), 31 FINALNEWLINE and 13 WHITESPACE. Line endings are left to git (ADR-1417), so there are no ENDOFLINE fixes. Run `dotnet format Curl.slnx --include <each path in touches>`, and change nothing outside touches. It is a formatting-only change; if `dotnet format` wants to change code beyond `using` order and whitespace, leave that file alone and say why under Notes.

This part names the 23 files one by one, because they are spread over 20 projects. The last one to finish of BL-6000 to BL-6005 also checks that `dotnet format Curl.slnx --verify-no-changes` exits 0 for the whole solution. Any file that has drifted meanwhile gets a new task.

## Acceptance criteria

- [ ] `dotnet format Curl.slnx --verify-no-changes --include <each path in touches>` exits 0.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-26: Created.
