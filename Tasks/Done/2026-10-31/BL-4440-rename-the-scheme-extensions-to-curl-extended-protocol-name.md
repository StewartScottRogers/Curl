---
id: BL-4440
title: Rename the scheme extensions to Curl.Extended.Protocol.<Name>
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extended.Protocol.Amqp0.UnitLibrary, Curl.Extended.Protocol.Amqp0.UnitTests, Curl.Extended.Protocol.Amqp1.UnitLibrary, Curl.Extended.Protocol.Amqp1.UnitTests, Curl.Extended.Protocol.AzureStorage.UnitLibrary, Curl.Extended.Protocol.AzureStorage.UnitTests, Curl.Extended.Protocol.Coap.UnitLibrary, Curl.Extended.Protocol.Coap.UnitTests, Curl.Extended.Protocol.DnsQuery.UnitLibrary, Curl.Extended.Protocol.DnsQuery.UnitTests, Curl.Extended.Protocol.GoogleCloudStorage.UnitLibrary, Curl.Extended.Protocol.GoogleCloudStorage.UnitTests, Curl.Extended.Protocol.Grpc.UnitLibrary, Curl.Extended.Protocol.Grpc.UnitTests, Curl.Extended.Protocol.Kafka.UnitLibrary, Curl.Extended.Protocol.Kafka.UnitTests, Curl.Extended.Protocol.McpClient.IntegrationTests, Curl.Extended.Protocol.McpClient.UnitLibrary, Curl.Extended.Protocol.McpClient.UnitTests, Curl.Extended.Protocol.Mqtt5.UnitLibrary, Curl.Extended.Protocol.Mqtt5.UnitTests, Curl.Extended.Protocol.Nats.UnitLibrary, Curl.Extended.Protocol.Nats.UnitTests, Curl.Extended.Protocol.OciRegistry.UnitLibrary, Curl.Extended.Protocol.OciRegistry.UnitTests, Curl.Extended.Protocol.Plugin.UnitLibrary, Curl.Extended.Protocol.Plugin.UnitTests, Curl.Extended.Protocol.Redis.UnitLibrary, Curl.Extended.Protocol.Redis.UnitTests, Curl.Extended.Protocol.S3.UnitLibrary, Curl.Extended.Protocol.S3.UnitTests, Curl.Extended.Protocol.SignalR.UnitLibrary, Curl.Extended.Protocol.SignalR.UnitTests, Curl.Extended.Protocol.SocketIo.UnitLibrary, Curl.Extended.Protocol.SocketIo.UnitTests, Curl.Extended.Protocol.Stomp.UnitLibrary, Curl.Extended.Protocol.Stomp.UnitTests, Curl.Extended.Protocol.WebTransport.UnitLibrary, Curl.Extended.Protocol.WebTransport.UnitTests, Curl.Extended.Protocol.Xmpp.UnitLibrary, Curl.Extended.Protocol.Xmpp.UnitTests, Curl.Extension.Composition.UnitLibrary, Directory.Build.props, Curl.slnx]
requirement: none
lane: no
created: 2026-10-31
completed: 2026-10-31
---
# BL-4440 — Rename the scheme extensions to Curl.Extended.Protocol.<Name>

## Goal

The 20 extensions that serve their own URL scheme are named `Curl.Extended.Protocol.<Name>.*` and sit under `/Extended/Protocols/<Name>/` in `Curl.slnx`, the counterpart of curl's own `Curl.Original.Protocol.<Name>`; every other extension keeps `Curl.Extension.<Name>`.

## Context

Stewart, 2026-10-31: rename `Curl.Extension.Protocol.*` (BL-4438, ADR-1174) to `Curl.Extended.Protocol.*`, with `/Extended/Protocols/<Name>/`, leaving the 42 option extensions, Abstractions and Composition as `Curl.Extension.*`. Three things find extensions by the `Curl.Extension.` prefix and must cover the new one: Composition's wildcard `ProjectReference` and `Directory.Build.props`'s `_IsExtensionProject` and `_ExtensionReference`.

## Acceptance criteria

- [x] Each of the 20 (41 projects) is `Curl.Extended.Protocol.<Name>.*` in folder, csproj, assembly and namespace, under `/Extended/Protocols/<Name>/` in `Curl.slnx`.
- [x] Composition references them by wildcard, and `VerifyExtensionIsolation` still treats them as extensions: one given a reference to a curl protocol or to another extension fails the build.
- [x] `curl --extensions` lists the same 62 extensions as before.
- [x] No live file outside the audit and gap paths names `Curl.Extension.Protocol`; ADR-1174 is superseded by an ADR naming the new rule, and `CLAUDE.md`, `LaneRules.md` and the glossary state it.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-10-31 (interactive): 41 folders moved, about 920 files changed. Build clean; fast tests 114 of 114 projects pass; `curl --extensions` identical (62 lines). `Curl.Extended.Protocol.Coap` given a reference to `Curl.Original.Protocol.Http`, to `Curl.Extension.Har` and to `Curl.Extended.Protocol.Redis` failed the build each time (ADR-0999). Composition gains a second wildcard reference, `Directory.Build.props` a second prefix in `_IsExtensionProject` and `_ExtensionReference`, and the ADR index the rows for 1174, 1176 and 1177.

## Log

- 2026-10-31: Created.
- 2026-10-31: Backlog -> Doing. Criteria at claim: ee39f02c 79a01002 23abc1ea df2e5a51 3d836d5e.
- 2026-10-31: Doing -> Done. scheme extensions renamed to Curl.Extended.Protocol under /Extended/Protocols/ (ADR-1177); build clean, 114 of 114 test projects pass, isolation rules cover the new prefix
