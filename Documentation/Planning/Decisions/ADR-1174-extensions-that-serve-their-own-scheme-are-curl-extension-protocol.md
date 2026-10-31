# ADR-1174: Extensions that serve their own URL scheme are named Curl.Extension.Protocol.<Name>

- Status: Accepted
- Date: 2026-10-31
- Asked for by Stewart (2026-10-31: rename the extension projects to `Curl.Extension.Protocol.*`, scheme
  extensions only, under an `/Extensions/Protocol/` solution folder); the dividing rule decided by Claude
  under Stewart's delegation (BL-4438)

## Context

ADR-0999 put everything Curl adds beyond upstream curl in `Curl.Extension.<Name>` projects. By October 2026
there were 62 of them, of two kinds: those that add a URL scheme curl does not have (`coap://`, `kafka://`,
`s3://` and the like) and those that add an option to curl's own schemes (`--har`, `--checksum`,
`--wireguard`). Their names did not say which kind a project was.

## Decision

An extension that serves its own URL scheme - one whose protocol handler is an entry in
`Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs` - is a *protocol extension*, named
`Curl.Extension.Protocol.<Name>.UnitLibrary` (with `.UnitTests` and, when it has them, `.IntegrationTests`),
namespace `Curl.Extension.Protocol.<Name>`, in `/Extensions/Protocol/<Name>/` in `Curl.slnx`. Every other
extension keeps `Curl.Extension.<Name>`, as do `Curl.Extension.Abstractions` and
`Curl.Extension.Composition`.

The rule is what the extension does, not what its specification is called: WireGuard, MASQUE, gRPC-Web,
Connect, JSON-RPC, GraphQL, Server-Sent Events and the WebSocket extensions are protocols by name but run over
curl's own schemes through an option, so they stay `Curl.Extension.<Name>`.

The 20 renamed in BL-4438: Amqp0, Amqp1, AzureStorage, Coap, DnsQuery, GoogleCloudStorage, Grpc, Kafka,
McpClient, Mqtt5, Nats, OciRegistry, Plugin (was PluginProtocol, which would have read
`Protocol.PluginProtocol`), Redis, S3, SignalR, SocketIo, Stomp, WebTransport, Xmpp. Type names
(`PluginProtocolExtension` and the rest) are unchanged.

## Consequences

- ADR-0999's isolation rules and ADR-1003's wildcard reference and one-line `Curl.slnx` entry apply
  unchanged: `Curl.Extension.*.UnitLibrary` still matches every extension.
- Inside the `Curl.Extension.Protocol` namespace a partly qualified `Protocol.Abstractions.X` resolves to
  `Curl.Extension.Protocol`, so extensions write `Curl.Protocol.Abstractions.X` in full.
- Task files in `Tasks/Done` and audit and gap findings keep the old names as history.
