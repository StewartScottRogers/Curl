# ADR-1091 — The amqp0 extension registers amqp and amqps and follows AMQP 0-9-1 with RabbitMQ's errata

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4117.

## Context

BL-4259 built the AMQP 0-9-1 codec (ADR-1056) and BL-4260 the client flow (ADR-1060) in
`Curl.Extension.Protocol.Amqp0.UnitLibrary`. BL-4117 registers it as an extension (ADR-0999) so `curl` serves its URLs, lists
it in `curl --extensions` and `curl --ai-help extensions` (ADR-1008), and stays out of the way with `CURL_EXTENSIONS=0`.

## Decision

- **Names.** The extension is `amqp0` (the `0` tells it apart from AMQP 1.0, `Curl.Extension.Protocol.Amqp1`, BL-4118). Its
  schemes are `amqp` (port 5672) and `amqps` (port 5671, TLS), the schemes of RabbitMQ's URI specification. It adds no
  command-line option: the URL path names the virtual host and queue, `-d` chooses publish, and `--user` the login
  (ADR-1060).
- **Specification.** AMQP 0-9-1 (2008-11-13) with RabbitMQ's errata and the class and method ids of
  `amqp0-9-1.extended.xml`; the description links RabbitMQ's copy of the specification.
- **Registration.** `Amqp0Extension.CreateHandler` in `ExtensionCatalogue` and `Amqp0Extension.Description` in
  `ExtensionDescriptionCatalogue`, one line each. With `CURL_EXTENSIONS=0` no extension handler is registered, so
  `amqp://` and `amqps://` fail with exit 1 and `Protocol "amqp" not supported`, exactly as curl fails any scheme it
  does not know.

## Left out, and why

- **Exchanges, routing keys, bindings and message properties.** A publish goes to the default exchange with the
  queue as routing key (ADR-1060); that delivers to the named queue, which is all a curl-style "send this there"
  needs. A URL query form can add them later without changing this one.
- **basic.consume, acknowledgements and transactions.** basic.get with no-ack takes one message, as `curl` takes one
  resource; a long-lived consumer is not a transfer.
- **SASL mechanisms other than PLAIN, and heartbeats.** RabbitMQ offers PLAIN by default, and a single short
  transfer needs no heartbeat (heartbeat 0 in tune-ok).

## Consequences

`amqp://host/%2F/jobs` gets one message from queue `jobs` on the default virtual host, and `-d` publishes to it. The
extension is tested against the specification with scripted connections, never against curl, which has no AMQP.
