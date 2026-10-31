---
id: BL-4438
title: Rename the 20 scheme extensions to Curl.Extension.Protocol.<Name>
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extension.Protocol.Amqp0.UnitLibrary, Curl.Extension.Protocol.Amqp0.UnitTests, Curl.Extension.Protocol.Amqp1.UnitLibrary, Curl.Extension.Protocol.Amqp1.UnitTests, Curl.Extension.Protocol.AzureStorage.UnitLibrary, Curl.Extension.Protocol.AzureStorage.UnitTests, Curl.Extension.Protocol.Coap.UnitLibrary, Curl.Extension.Protocol.Coap.UnitTests, Curl.Extension.Protocol.DnsQuery.UnitLibrary, Curl.Extension.Protocol.DnsQuery.UnitTests, Curl.Extension.Protocol.GoogleCloudStorage.UnitLibrary, Curl.Extension.Protocol.GoogleCloudStorage.UnitTests, Curl.Extension.Protocol.Grpc.UnitLibrary, Curl.Extension.Protocol.Grpc.UnitTests, Curl.Extension.Protocol.Kafka.UnitLibrary, Curl.Extension.Protocol.Kafka.UnitTests, Curl.Extension.Protocol.McpClient.IntegrationTests, Curl.Extension.Protocol.McpClient.UnitLibrary, Curl.Extension.Protocol.McpClient.UnitTests, Curl.Extension.Protocol.Mqtt5.UnitLibrary, Curl.Extension.Protocol.Mqtt5.UnitTests, Curl.Extension.Protocol.Nats.UnitLibrary, Curl.Extension.Protocol.Nats.UnitTests, Curl.Extension.Protocol.OciRegistry.UnitLibrary, Curl.Extension.Protocol.OciRegistry.UnitTests, Curl.Extension.Protocol.Plugin.UnitLibrary, Curl.Extension.Protocol.Plugin.UnitTests, Curl.Extension.Protocol.Redis.UnitLibrary, Curl.Extension.Protocol.Redis.UnitTests, Curl.Extension.Protocol.S3.UnitLibrary, Curl.Extension.Protocol.S3.UnitTests, Curl.Extension.Protocol.SignalR.UnitLibrary, Curl.Extension.Protocol.SignalR.UnitTests, Curl.Extension.Protocol.SocketIo.UnitLibrary, Curl.Extension.Protocol.SocketIo.UnitTests, Curl.Extension.Protocol.Stomp.UnitLibrary, Curl.Extension.Protocol.Stomp.UnitTests, Curl.Extension.Protocol.WebTransport.UnitLibrary, Curl.Extension.Protocol.WebTransport.UnitTests, Curl.Extension.Protocol.Xmpp.UnitLibrary, Curl.Extension.Protocol.Xmpp.UnitTests, Curl.Extension.Composition.UnitLibrary, Curl.Extension.Composition.UnitTests, Curl.slnx]
requirement: none
lane: no
created: 2026-10-31
completed:
---
# BL-4438 — Rename the 20 scheme extensions to Curl.Extension.Protocol.<Name>

## Goal

The 20 extensions that serve their own URL scheme (the entries of `ExtensionCatalogue.cs`) are named `Curl.Extension.Protocol.<Name>.*` and sit under `/Extensions/Protocol/<Name>/` in `Curl.slnx`; the 46 option extensions, Abstractions and Composition keep their names.

## Context

Stewart, 2026-10-31: rename `Curl.Extension.*` to `Curl.Extension.Protocol.*`, option A (scheme extensions only, with the `/Extensions/Protocol/` folder). Renamed: Amqp0, Amqp1, AzureStorage, Coap, DnsQuery, GoogleCloudStorage, Grpc, Kafka, McpClient, Mqtt5, Nats, OciRegistry, PluginProtocol (as `Plugin`), Redis, S3, SignalR, SocketIo, Stomp, WebTransport, Xmpp. Interactive only: it moves 41 project folders at once. ADR-0999 and ADR-1003 describe the extension layout.

## Acceptance criteria

- [x] Each of the 20 projects (and its tests) is `Curl.Extension.Protocol.<Name>.*` in folder, csproj, assembly and namespace; PluginProtocol is `Curl.Extension.Protocol.Plugin`.
- [x] `Curl.slnx` holds them under `/Extensions/Protocol/<Name>/`.
- [x] No live file (code, docs, `CLAUDE.md`, `LaneRules.md`, open tasks) names an old project; `Tasks/Done`, audit findings and scorecards keep history.
- [x] An ADR records the rule (an extension that serves its own scheme is a protocol extension).
- [x] `dotnet build` is clean and the fast tests pass; `curl --extensions` lists the same 62 extensions as before.

## Notes

- 2026-10-31 (interactive): 41 folders moved, 938 files changed. Build clean; fast tests 114 of 114 projects pass; `curl --extensions` identical before and after (62 lines). Inside `Curl.Extension.Protocol` a partly qualified `Protocol.Abstractions` resolved to the new namespace, so 3 extension files now write `Curl.Protocol.Abstractions` in full, and Composition's catalogues name the renamed ones as `Protocol.<Name>`. The quality gates are left to CI's quality job (no code changed but names).

## Log

- 2026-10-31: Created.
- 2026-10-31: Backlog -> Doing. Criteria at claim: e72a470e 46035b10 a14e76c8 53b39f26 0f6b68ec.
