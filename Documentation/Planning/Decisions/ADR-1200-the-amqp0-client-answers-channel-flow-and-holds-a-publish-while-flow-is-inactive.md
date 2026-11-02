# ADR-1200: The AMQP 0-9-1 client answers channel.flow and holds a publish while flow is inactive

- Status: Accepted
- Date: 2026-11-02
- Task: BL-4465 (measured), BL-4571 (fix)
- Decided by Claude under Stewart's delegation

## Context

AMQP 0-9-1 section 1.5.2.3 lets a broker send channel.flow (20.20) to stop (active 0) or restart (active 1) the
content a peer sends on a channel; the peer answers with channel.flow-ok (20.21) carrying the same active bit.
BL-4465's conformance case `Amqp0MessagingTests.Flow_FromTheBroker_IsAnsweredWithFlowOk` (requirement
`Amqp0:AMQP091-1.5.2.3-1`) found that `Amqp0MethodReader` did not know 20.20, so a channel.flow arriving while the
client waited for a reply ended the transfer with exit 8 and a connection.close 503. Upstream curl has no AMQP, so
there is no curl behaviour to match.

## Decision

`Amqp0MethodReader` reads channel.flow and channel.flow-ok into `Amqp0ChannelFlow` and `Amqp0ChannelFlowOk`.
Wherever `Amqp0ClientFlow` waits for a method, a channel.flow is answered at once with channel.flow-ok carrying the
same active bit, its active bit is remembered, and the client goes on waiting for the reply it expected.

When the publish is due and the last channel.flow said active 0, the client sends no content: it waits, answering
every further channel.flow, until one says active 1, and then publishes. A channel.close or connection.close arriving
meanwhile ends the transfer as any server close does; any other method ends it as an unexpected method (exit 8).
Waiting rather than failing is what the specification asks of a peer told to pause, and a broker that pauses a
publisher (RabbitMQ under a memory alarm) expects it to resume; `--max-time` still bounds the wait. A basic.get
sends no content, so flow does not hold it.

## Consequences

- A publish to a paused broker completes once the broker restarts flow, with exit 0.
- The client never sends channel.flow itself.
