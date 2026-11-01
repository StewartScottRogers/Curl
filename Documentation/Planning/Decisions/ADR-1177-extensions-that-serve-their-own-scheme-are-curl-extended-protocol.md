# ADR-1177: Extensions that serve their own URL scheme are named Curl.Extended.Protocol.<Name>

- Status: Accepted
- Date: 2026-10-31
- Supersedes ADR-1174
- Asked for by Stewart (2026-10-31: rename `Curl.Extension.Protocol.*` to `Curl.Extended.Protocol.*`, with
  `/Extended/Protocols/<Name>/`, leaving every other extension as `Curl.Extension.*`); carried out in BL-4440

## Context

ADR-1174 named the extensions that serve their own URL scheme `Curl.Extension.Protocol.<Name>`, and
ADR-1176 then named curl's own protocols `Curl.Original.Protocol.<Name>`. The two kinds of protocol did not
read as a pair.

## Decision

An extension that serves its own URL scheme - one whose protocol handler is an entry in
`Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs` - is an *extended protocol*, named
`Curl.Extended.Protocol.<Name>.UnitLibrary` (with `.UnitTests` and, for McpClient, `.IntegrationTests`),
namespace `Curl.Extended.Protocol.<Name>`, in `/Extended/Protocols/<Name>/` in `Curl.slnx`, beside
`/Original/Protocols/<Name>/`: Amqp0, Amqp1, AzureStorage, Coap, DnsQuery, GoogleCloudStorage, Grpc, Kafka,
McpClient, Mqtt5, Nats, OciRegistry, Plugin, Redis, S3, SignalR, SocketIo, Stomp, WebTransport and Xmpp.

Every other extension, and `Curl.Extension.Abstractions` and `Curl.Extension.Composition`, keeps
`Curl.Extension.<Name>`. An extended protocol is still an extension under ADR-0999.

## Consequences

- What finds extensions by name covers both prefixes: Composition references `Curl.Extension.*.UnitLibrary`
  and `Curl.Extended.Protocol.*.UnitLibrary` by wildcard (ADR-1003), and `VerifyExtensionIsolation` in
  `Directory.Build.props` treats a `Curl.Extended.Protocol.*` project, and a reference to one, as an
  extension's.
- Composition's catalogues name each extended protocol in full (`Curl.Extended.Protocol.Coap.Client...`),
  since the short name no longer sits under `Curl.Extension`.
- Task files in `Tasks/Done` and audit findings keep the old names as history.
