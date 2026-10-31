---
id: BL-4440
title: Rename the scheme extensions to Curl.Extended.Protocol.<Name>
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extension.Protocol.Amqp0.UnitLibrary, Curl.Extension.Protocol.Amqp0.UnitTests, Curl.Extension.Protocol.Amqp1.UnitLibrary, Curl.Extension.Protocol.Amqp1.UnitTests, Curl.Extension.Protocol.AzureStorage.UnitLibrary, Curl.Extension.Protocol.AzureStorage.UnitTests, Curl.Extension.Protocol.Coap.UnitLibrary, Curl.Extension.Protocol.Coap.UnitTests, Curl.Extension.Protocol.DnsQuery.UnitLibrary, Curl.Extension.Protocol.DnsQuery.UnitTests, Curl.Extension.Protocol.GoogleCloudStorage.UnitLibrary, Curl.Extension.Protocol.GoogleCloudStorage.UnitTests, Curl.Extension.Protocol.Grpc.UnitLibrary, Curl.Extension.Protocol.Grpc.UnitTests, Curl.Extension.Protocol.Kafka.UnitLibrary, Curl.Extension.Protocol.Kafka.UnitTests, Curl.Extension.Protocol.McpClient.IntegrationTests, Curl.Extension.Protocol.McpClient.UnitLibrary, Curl.Extension.Protocol.McpClient.UnitTests, Curl.Extension.Protocol.Mqtt5.UnitLibrary, Curl.Extension.Protocol.Mqtt5.UnitTests, Curl.Extension.Protocol.Nats.UnitLibrary, Curl.Extension.Protocol.Nats.UnitTests, Curl.Extension.Protocol.OciRegistry.UnitLibrary, Curl.Extension.Protocol.OciRegistry.UnitTests, Curl.Extension.Protocol.Plugin.UnitLibrary, Curl.Extension.Protocol.Plugin.UnitTests, Curl.Extension.Protocol.Redis.UnitLibrary, Curl.Extension.Protocol.Redis.UnitTests, Curl.Extension.Protocol.S3.UnitLibrary, Curl.Extension.Protocol.S3.UnitTests, Curl.Extension.Protocol.SignalR.UnitLibrary, Curl.Extension.Protocol.SignalR.UnitTests, Curl.Extension.Protocol.SocketIo.UnitLibrary, Curl.Extension.Protocol.SocketIo.UnitTests, Curl.Extension.Protocol.Stomp.UnitLibrary, Curl.Extension.Protocol.Stomp.UnitTests, Curl.Extension.Protocol.WebTransport.UnitLibrary, Curl.Extension.Protocol.WebTransport.UnitTests, Curl.Extension.Protocol.Xmpp.UnitLibrary, Curl.Extension.Protocol.Xmpp.UnitTests, Curl.Extension.Composition.UnitLibrary, Directory.Build.props, Curl.slnx]
requirement: none
lane: no
created: 2026-10-31
completed:
---
# BL-4440 — Rename the scheme extensions to Curl.Extended.Protocol.<Name>

## Goal

The 20 extensions that serve their own URL scheme are named `Curl.Extended.Protocol.<Name>.*` and sit under `/Extended/Protocols/<Name>/` in `Curl.slnx`, the counterpart of curl's own `Curl.Original.Protocol.<Name>`; every other extension keeps `Curl.Extension.<Name>`.

## Context

Stewart, 2026-10-31: rename `Curl.Extension.Protocol.*` (BL-4438, ADR-1174) to `Curl.Extended.Protocol.*`, with `/Extended/Protocols/<Name>/`, leaving the 42 option extensions, Abstractions and Composition as `Curl.Extension.*`. Three things find extensions by the `Curl.Extension.` prefix and must cover the new one: Composition's wildcard `ProjectReference` and `Directory.Build.props`'s `_IsExtensionProject` and `_ExtensionReference`.

## Acceptance criteria

- [ ] Each of the 20 (41 projects) is `Curl.Extended.Protocol.<Name>.*` in folder, csproj, assembly and namespace, under `/Extended/Protocols/<Name>/` in `Curl.slnx`.
- [ ] Composition references them by wildcard, and `VerifyExtensionIsolation` still treats them as extensions: one given a reference to a curl protocol or to another extension fails the build.
- [ ] `curl --extensions` lists the same 62 extensions as before.
- [ ] No live file outside the audit and gap paths names `Curl.Extension.Protocol`; ADR-1174 is superseded by an ADR naming the new rule, and `CLAUDE.md`, `LaneRules.md` and the glossary state it.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-31: Created.
- 2026-10-31: Backlog -> Doing. Criteria at claim: ee39f02c 79a01002 23abc1ea df2e5a51 3d836d5e.
