# ADR-1284: A STOMP CONNECT names the virtual host `/` unless -H "host: ..." gives another, and an ERROR shows its body

- Status: Accepted
- Date: 2026-11-16
- Decided by Claude under Stewart's delegation (BL-5321)

## Context

`CONNECT` carried the URL's host as its `host` header. STOMP's `host` is the virtual host, so a stock RabbitMQ 4
broker answered `ERROR: Bad CONNECT` (`virtual host '127.0.0.1' does not exist`) and exit 67. The message also left
out the ERROR frame's body, where brokers put the reason. STOMP is an extension (ADR-0999): there is no upstream curl
behaviour to match.

## Decision

- `CONNECT`'s `host` is `/` by default, the default virtual host of RabbitMQ and other stock brokers.
- A `-H "host: <vhost>"` (name matched without regard to case, last wins, empty value ignored) names another virtual
  host, as `-H "content-type: ..."` does for `SEND` (ADR-1220). No new option, no URL syntax: the URL path stays the
  destination.
- An `ERROR` frame with a non-empty body is reported as `STOMP server sent ERROR: <message>: <body>` (body trimmed);
  one without a body keeps `STOMP server sent ERROR: <message>`. Exit codes are unchanged (ADR-1251).

## Consequences

`StompProtocolHandler.VirtualHostOf` reads the header; `StompClientFlow` builds the message. The STOMP row of
`Documentation/Protocols.md`, the conformance row `Stomp:STOMP12-Connect-2` and `--extended-help` state the rule.
