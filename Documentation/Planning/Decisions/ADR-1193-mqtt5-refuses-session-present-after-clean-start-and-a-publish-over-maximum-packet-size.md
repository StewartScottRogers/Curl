# ADR-1193: The mqtt5:// client refuses Session Present after Clean Start and a PUBLISH over the server's Maximum Packet Size

- Status: Accepted
- Date: 2026-11-02
- Decided by Claude under Stewart's delegation (BL-4518)

## Context

BL-4518 measures CONNACK handling on `mqtt5://` against OASIS MQTT Version 5.0 section 3.2. Two client
statements need a failure that ADR-1049's table does not yet name:

- MQTT-3.2.2-4: a client without Session State that receives Session Present 1 MUST close the Network
  Connection. curl always sends Clean Start and keeps no session.
- MQTT-3.2.2-15: the client MUST NOT send a packet over the CONNACK's Maximum Packet Size.

Upstream curl's `mqtt://` speaks MQTT 3.1.1, which has neither, so there is no curl output to match.

## Decision

- Session Present 1 on a CONNACK answering curl's CONNECT ends the transfer with exit 8
  `CURLE_WEIRD_SERVER_REPLY`, the code ADR-1049 gives every CONNACK curl cannot accept, and the text
  `MQTT 5 CONNACK Session Present on a Clean Start`, sending nothing after CONNECT.
- A PUBLISH whose framed length exceeds the Maximum Packet Size is not sent: exit 55 `CURLE_SEND_ERROR`,
  since the data cannot be sent to the peer, with `MQTT 5 PUBLISH of <n> bytes exceeds the server's Maximum
  Packet Size of <m>`, `<n>` the whole packet's length as section 3.2.2.3.6 counts it.

`ConnackCases` in `Curl.Conformance.Extended.UnitTests` pins both; BL-4560 and BL-4561 make the client do it.

## Consequences

A script sees a refused broker the way it sees any other refused CONNACK, and an oversized message fails
loudly rather than being dropped by the broker.
