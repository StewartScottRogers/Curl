# ADR-1312: The amqp client picks PLAIN, ANONYMOUS or AMQPLAIN from what the server offers

- Status: Accepted
- Date: 2026-11-19
- Task: BL-5568
- Decided by Claude under Stewart's delegation.

## Context

The `amqp://` client answered `Connection.Start` with PLAIN only and exited 67 against a server that did not offer it (bug report 2026-11-19T0930, finding 5).

## Decision

The client takes the first of these the server lists in `mechanisms`:

1. PLAIN (as before; `guest`/`guest` when no user name is given).
2. ANONYMOUS, only when no user name was given; the response is empty.
3. AMQPLAIN: a field table body (no length prefix) of `LOGIN` and `PASSWORD` long strings, with `guest`/`guest` when no user name is given.

PLAIN stays first so an existing login behaves exactly as before. ANONYMOUS is never used when `-u` was given, so a user name is not silently dropped. When the server offers none of them the client exits 67 naming the offered list and sends no credentials. EXTERNAL needs a TLS client certificate the AMQP handler does not receive, and CRAM-MD5 needs a challenge/secure round the flow does not have, so both are left out for now; the `Curl.Authentication` pieces were not needed.

## Consequences

`Amqp0Mechanism.Choose` holds the order and the response bytes; tests pin each.
