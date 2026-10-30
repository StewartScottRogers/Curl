# Curl.Extension.Masque.UnitLibrary

The MASQUE extension (ADR-0999, ADR-1115): proxying UDP (CONNECT-UDP, RFC 9298) and IP
(CONNECT-IP, RFC 9484) through an HTTP proxy. It sits in `/Extensions/Masque/` in
`Curl.slnx` and references only `Curl.Protocol.Abstractions.UnitLibrary`,
`Curl.Extension.Abstractions.UnitLibrary` and the shared `Curl.HttpDatagram.UnitLibrary`
(capsules and variable-length integers, ADR-1004). It never references a `Curl.Protocol.*`
library or another extension, so it does not reuse `Curl.Networking`'s own CONNECT-UDP
request for `-x` (ADR-0250).

Namespace `Curl.Extension.Masque`, with `Wire` and `Tunnel` below it. `Wire/` is the wire
format; it works on bytes and owns no stream or socket. `Tunnel/` is the client flow (ADR-1122),
and `MasqueExtension` is the option (ADR-1149).

- `MasqueExtension` holds `--masque <uri-template>`, its description and `Activate`: a template naming
  `target_host` or `target_port` opens CONNECT-UDP, any other CONNECT-IP, and one that does not expand is
  refused with an `ArgumentException`. It is registered in `Curl.Extension.Composition`'s option, description and
  connection hook catalogues.
- `Tunnel/MasqueConnectionHook` keeps curl's stream connector and replaces curl's datagram connector with a
  `MasqueDatagramConnector`, so QUIC and TFTP run through the proxy. The engine takes the datagram connector before
  it hands over the stream connector, so the proxy is reached through a `LateBoundConnector` that
  `ConnectorOver` binds; an open before then fails with exit 7.

- `MasqueUriTemplate` expands a proxy's URI template with `target_host`/`target_port`
  or `target`/`ipproto`, percent-encoding values as RFC 6570 simple expansion does.
- `MasqueUpgrade` builds the HTTP/1.1 upgrade request and checks the proxy's reply
  (`101` with the `Upgrade` token and `Connection: Upgrade`, anything else refused).
- `ConnectIpCapsuleCodec` encodes and decodes `ADDRESS_ASSIGN`, `ADDRESS_REQUEST` and
  `ROUTE_ADVERTISEMENT`; `CapsuleValueReader` reads their fields.
- `IpUdpPacket` builds the IPv4 or IPv6 packet carrying one UDP datagram
  (`IpUdpDatagram`) and reads one back, ignoring any other packet.
- `MasqueDatagramPayload` puts Context ID 0 in front of a payload and takes it off.
- Everything a proxy or a template gets wrong is a `MasqueException`; its `StatusCode`
  is the status of a refused upgrade.
- `Tunnel/MasqueDatagramConnector` is the `IDatagramConnector`: it reaches the proxy through
  curl's `IConnector`, upgrades, and returns a `ConnectUdpChannel` (DATAGRAM capsules, Context
  ID 0) or, after `ADDRESS_REQUEST` and `ADDRESS_ASSIGN`, a `ConnectIpChannel` (IP/UDP packets
  from the assigned address). An unreachable proxy keeps the connector's exit code, every other
  open failure is exit 7, a CONNECT-IP host name exit 6. `MasqueTunnelStream` reads the reply
  head and capsules over the `IConnection`; a tunnel the proxy closes fails a receive as
  `SocketError.ConnectionReset`. Tests drive it through `ScriptedProxyConnection` and
  `RecordingConnector` in `Curl.Extension.Masque.UnitTests/Tunnel`.
