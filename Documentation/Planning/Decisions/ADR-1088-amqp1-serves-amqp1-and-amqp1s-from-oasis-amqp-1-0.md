# ADR-1088 — AMQP 1.0 serves amqp1 and amqp1s, following OASIS AMQP Version 1.0

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4118.

## Context

BL-4118 is extension 17 of 63: an AMQP 1.0 client as `Curl.Extension.Amqp1.UnitLibrary` (ADR-0999). Its type codec
(ADR-1053), frames (ADR-1058) and client (ADR-1062) landed under BL-4258, BL-4256 and BL-4257; this task registers the
extension and records the names it takes, the specification it follows and what it leaves out. Extension 16
(BL-4117, `Curl.Extension.Amqp0.UnitLibrary`, AMQP 0-9-1) takes `amqp://` and `amqps://`, the names RabbitMQ and the
AMQP 0-9-1 URI specification use, so AMQP 1.0 must take other names.

## Decision

- **Schemes `amqp1` and `amqp1s`.** A version digit on the protocol name follows the `mqtt5` precedent and keeps
  `amqp`/`amqps` for 0-9-1. Default ports 5672 and 5671 (Part 5, section 5.1 of the specification, and the IANA
  registration); `amqp1s://` gets TLS from curl's connector, not from the AMQP TLS upgrade header (protocol id 2).
- **No options of its own.** The address is the URL-decoded path, the request body (`-d`, `-T`) chooses sending,
  `--user` chooses SASL PLAIN, otherwise SASL ANONYMOUS.
- **Specification:** OASIS AMQP Version 1.0 (OASIS Standard, 29 October 2012), Parts 1 (types), 2 (transport),
  3 (messaging) and 5 (security, SASL only).
- **Description** in `curl --extensions` and `curl --ai-help extensions` from `Amqp1Extension.Description`, linking
  the specification overview.
- **Left out, and why:**
  - SASL mechanisms other than PLAIN and ANONYMOUS (EXTERNAL, SCRAM, GSSAPI): the task names those two, and curl's
    `--user` maps onto PLAIN; another mechanism waits for a task asking for it.
  - The in-band TLS upgrade (protocol header id 2): curl's connector already gives TLS on `amqp1s://`, which is how
    brokers serve 5671.
  - Transactions (Part 4), more than one link or delivery per transfer, and multi-message batching: one message per
    transfer keeps curl's one-URL-one-transfer shape (ADR-1062).
  - Message properties, headers and application properties on send: the body goes as one data section; nothing in
    curl's options names such fields.
  - AMQP over WebSocket: no task asks for it.

## Consequences

- `amqp1://broker/queue` and `amqp://broker/vhost/queue` reach different extensions with no ambiguity.
- With `CURL_EXTENSIONS=0` neither scheme is registered, so both fail as curl fails any unsupported scheme
  (exit 1, `Protocol "amqp1" not supported`).
