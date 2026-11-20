# ADR-1314: CoAP connects its UDP socket so an unbound port fails at once on every platform

- Status: Accepted
- Date: 2026-11-20
- Task: BL-5601
- Decided by Claude under Stewart's delegation.

## Context

`coap://127.0.0.1:<unbound>/` exits 7 at once on Windows, where an ICMP port unreachable surfaces as `ConnectionReset` on the next receive of an unconnected UDP socket. Linux (and macOS) report it only on a connected socket (`ConnectionRefused`), so the unconnected channel waited out CoAP's retransmissions. `UdpDatagramConnector` cannot connect every channel: TFTP's server answers from a new port, which a connected socket drops.

## Decision

`IDatagramChannel` gets a default member `ConnectToServer()` that does nothing. `UdpDatagramChannel` connects its socket to the server; `PortUnreachableReportingDatagramChannel` forwards the call and connects each alternative it switches to. `CoapProtocolHandler` calls it right after the channel opens, so the refusal reaches the receive as `ConnectionRefused` or `ConnectionReset`, which the wrapper already turns into exit 7. Retransmission timing (RFC 7252 section 4.2) is unchanged: a refusal is the only early end. TFTP and the other datagram users never call it.

## Consequences

The same call is available to any other protocol whose server answers from the address it was sent to. Unit tests pin the connect and its forwarding; an Integration test pins the refusal on a real loopback socket.
