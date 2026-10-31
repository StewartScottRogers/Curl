---
id: BL-4438
title: Rename the 20 scheme extensions to Curl.Extension.Protocol.<Name>
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extension.Amqp0.UnitLibrary, Curl.Extension.Amqp0.UnitTests, Curl.Extension.Amqp1.UnitLibrary, Curl.Extension.Amqp1.UnitTests, Curl.Extension.AzureStorage.UnitLibrary, Curl.Extension.AzureStorage.UnitTests, Curl.Extension.Coap.UnitLibrary, Curl.Extension.Coap.UnitTests, Curl.Extension.DnsQuery.UnitLibrary, Curl.Extension.DnsQuery.UnitTests, Curl.Extension.GoogleCloudStorage.UnitLibrary, Curl.Extension.GoogleCloudStorage.UnitTests, Curl.Extension.Grpc.UnitLibrary, Curl.Extension.Grpc.UnitTests, Curl.Extension.Kafka.UnitLibrary, Curl.Extension.Kafka.UnitTests, Curl.Extension.McpClient.IntegrationTests, Curl.Extension.McpClient.UnitLibrary, Curl.Extension.McpClient.UnitTests, Curl.Extension.Mqtt5.UnitLibrary, Curl.Extension.Mqtt5.UnitTests, Curl.Extension.Nats.UnitLibrary, Curl.Extension.Nats.UnitTests, Curl.Extension.OciRegistry.UnitLibrary, Curl.Extension.OciRegistry.UnitTests, Curl.Extension.PluginProtocol.UnitLibrary, Curl.Extension.PluginProtocol.UnitTests, Curl.Extension.Redis.UnitLibrary, Curl.Extension.Redis.UnitTests, Curl.Extension.S3.UnitLibrary, Curl.Extension.S3.UnitTests, Curl.Extension.SignalR.UnitLibrary, Curl.Extension.SignalR.UnitTests, Curl.Extension.SocketIo.UnitLibrary, Curl.Extension.SocketIo.UnitTests, Curl.Extension.Stomp.UnitLibrary, Curl.Extension.Stomp.UnitTests, Curl.Extension.WebTransport.UnitLibrary, Curl.Extension.WebTransport.UnitTests, Curl.Extension.Xmpp.UnitLibrary, Curl.Extension.Xmpp.UnitTests, Curl.Extension.Composition.UnitLibrary, Curl.Extension.Composition.UnitTests, Curl.slnx]
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

- [ ] Each of the 20 projects (and its tests) is `Curl.Extension.Protocol.<Name>.*` in folder, csproj, assembly and namespace; PluginProtocol is `Curl.Extension.Protocol.Plugin`.
- [ ] `Curl.slnx` holds them under `/Extensions/Protocol/<Name>/`.
- [ ] No live file (code, docs, `CLAUDE.md`, `LaneRules.md`, open tasks) names an old project; `Tasks/Done`, audit findings and scorecards keep history.
- [ ] An ADR records the rule (an extension that serves its own scheme is a protocol extension).
- [ ] `dotnet build` is clean and the fast tests pass; `curl --extensions` lists the same 63 extensions as before.

## Notes

## Log

- 2026-10-31: Created.
- 2026-10-31: Backlog -> Doing. Criteria at claim: e72a470e 46035b10 a14e76c8 53b39f26 0f6b68ec.
