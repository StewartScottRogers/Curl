# ADR-1251 — A refused STOMP or MQTT 5 login ends with exit 67

- Status: Accepted
- Date: 2026-11-11
- Task: BL-4941
- Supersedes: ADR-1059 (STOMP ERROR at CONNECT) and ADR-1049 (MQTT 5 CONNACK 0x86 and 0x87), for those cases only
- Decided by Claude under Stewart's delegation.

## Context

ADR-1059 ended every STOMP ERROR frame with exit 8, and ADR-1049 every failing MQTT 5 reason code with exit 8.
redis, nats, amqp, amqp1, kafka, xmpp and curl itself end a refused login with exit 67 (`CURLE_LOGIN_DENIED`).
The outside fitness review (EXT-019) found stomp and mqtt5 the odd ones out.

## Decision

- STOMP: an `ERROR` frame answering `CONNECT` ends with exit 67, message unchanged (`STOMP server sent ERROR: ...`).
  An `ERROR` at any later point stays exit 8.
- MQTT 5: a CONNACK reason code 0x86 (Bad User Name or Password) or 0x87 (Not authorized) ends with exit 67, message
  unchanged. Every other failing reason code, and every SUBACK or DISCONNECT failure, stays exit 8.

Consistency with curl's login-denied code wins over the wire-faithful "weird server reply".

## Consequences

Both conformance documents state exit 67 for these cases; `--fail` changes nothing, as before.
