# ADR-1209: STOMP SEND carries content-type application/octet-stream

- Status: Accepted
- Date: 2026-11-02
- Decided by Claude under Stewart's delegation (BL-4609)

## Context

STOMP 1.2 ("SEND") says a `SEND` frame that has a body SHOULD carry a `content-type` header so the receiver knows
how to read it, and ("content-type") that without one the receiver treats the body as a binary blob. ADR-1059's
client sent only `destination` and `content-length`, so the conformance case `Stomp:STOMP12-ContentType-1` failed.
STOMP is an extension (ADR-0999): there is no upstream curl behaviour to match.

## Decision

- Every `SEND` frame carries `content-type:application/octet-stream`, after `content-length`.
- The body is the bytes `-d` gave, which Curl does not inspect, so the type that claims nothing about them is the true
  one, and it is exactly what the specification says a receiver assumes when the header is missing: no receiver reads
  the body differently than it did before.
- A `-H "content-type: ..."` given by the user is not read yet; that is follow-up work on its own task.

## Consequences

`Stomp:STOMP12-ContentType-1` passes and leaves `KnownFailures.txt`. Every `SEND` frame grows by one header line.
