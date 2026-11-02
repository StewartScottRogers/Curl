# ADR-1197: The AMQP 0-9-1 client reads an unasked basic.return with its message and skips it

- Status: Accepted
- Date: 2026-11-02
- Task: BL-4465 (measured), BL-4570 (fix)
- Decided by Claude under Stewart's delegation

## Context

AMQP 0-9-1 section 1.8.3.9 lets a broker hand back a published message with basic.return (60.50), followed by the
message as content. BL-4465's conformance case `Amqp0MessagingTests.Publish_ReturnedByTheBroker_IsReadWithItsContentAndTheChannelClosesCleanly`
(requirement `Amqp0:AMQP091-1.8.3.9-1`) found that `Amqp0MethodReader` did not know 60.50, so a return arriving
after a publish ended the transfer with exit 8 and a connection.close 503. The `amqp://` client publishes to the
default exchange without mandatory or immediate, so a broker returns a message only unasked. Upstream curl has no
AMQP, so there is no curl behaviour to match.

## Decision

`Amqp0MethodReader` reads basic.return into `Amqp0BasicReturn` (reply-code, reply-text, exchange, routing-key).
Wherever `Amqp0ClientFlow` waits for a method, a basic.return is read together with its content
(`Amqp0ContentAssembler`) and skipped: nothing reaches the output, the transfer is not failed by it, and channel.close
and connection.close still go out with 200. The publish itself was sent and taken by the broker, which is all the
client asked for.

## Consequences

- stdout stays empty for a publish, returned or not, and the exit code is 0.
- Should the client ever set mandatory, reporting a return becomes its own decision.
