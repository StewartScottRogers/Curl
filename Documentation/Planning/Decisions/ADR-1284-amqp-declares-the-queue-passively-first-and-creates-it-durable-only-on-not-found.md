# ADR-1284 — amqp:// declares the queue passively first and creates it durable only on NOT_FOUND

- Status: Accepted
- Date: 2026-11-16
- Task: BL-5320
- Decided by Claude under Stewart's delegation.

## Context

The client always sent a non-passive, non-durable `queue.declare`. RabbitMQ 4 refuses transient non-exclusive
queues (541) and answers 406 for an existing durable queue.

## Decision

The first `queue.declare` is passive. A declare-ok means the queue is used as it is. A `channel.close` with reply
code 404 is answered with `channel.close-ok`, the channel is opened again and a durable, non-passive
`queue.declare` follows (for `basic.get` too, so one path serves both). Any other close ends the transfer as before
(403 exit 67, others exit 8).

## Consequences

Existing queues work whatever their arguments; missing queues are created durable. Verified only against scripted
frames; no real broker runs in a lane.
