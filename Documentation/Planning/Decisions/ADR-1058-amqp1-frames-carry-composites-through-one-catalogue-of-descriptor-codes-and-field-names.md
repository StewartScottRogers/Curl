# ADR-1058 — AMQP 1.0 frames carry composites through one catalogue of descriptor codes and field names

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4256.

## Context

BL-4256 adds the transport of OASIS AMQP Version 1.0 to `Curl.Extension.Amqp1.UnitLibrary`, on the type system of
ADR-1053: the protocol headers, the frame layout, nine performatives, the error type, five delivery states, five SASL
frame bodies and the message sections. Every one of these but the non-list sections is a composite type: a described
list whose fields are positional, some mandatory. Upstream curl has no AMQP, so the specification decides.

## Decision

- **One catalogue, one value type.** `Amqp1CompositeType` lists every composite a client writes or reads (the
  performatives, error, received, accepted, rejected, released, modified, source, target, header, properties and the
  SASL frame bodies) with its name, numeric descriptor code, kind and field names in wire order, a trailing `!`
  marking a mandatory field. `Amqp1Composite` is one value of such a type, its fields read by name. A record per
  composite would be two dozen near-identical types for the same rule; the catalogue keeps each definition to the
  specification's field list, which a reader checks line by line against the specification.
- **Write numeric, read either.** A composite is written with its numeric descriptor (`00 53 10` for open, the form
  brokers send); a symbolic descriptor (`amqp:open:list`) reads as the same type. A descriptor outside the catalogue,
  a value that is not a list and a mandatory field absent or null raise `Amqp1MalformedEncodingException`, the
  library's one exception, on reading and on building a value alike.
- **Trailing nulls are left off.** `Amqp1Composite.Create` ends the list at the last field set, as Part 1 section
  1.4 allows, so `end` with no error is `00 53 17 45`.
- **A frame carries only its own kind.** An AMQP frame carries a performative and a SASL frame a SASL frame body;
  only a transfer is followed by a payload. A frame breaking either is malformed, read or written.
- **Frames are read from a buffer, not a stream.** `Amqp1FrameCodec.TryRead` answers false until the buffer holds a
  whole frame, so the client (BL-4257) owns all I/O; the extended header is skipped and DOFF 2 is always written. The
  limit of the negotiated max-frame-size is the client's to enforce, since only it knows the value.
- **Sections are values of their kind.** `Amqp1Section` pairs an `Amqp1SectionType` with its value: an
  `Amqp1Composite` for header and properties, an `Amqp1Map` for the annotations, application-properties and footer,
  `byte[]` for data, a list for amqp-sequence and any value for amqp-value. `Amqp1MessageCodec.Read` joins the
  payloads of every transfer of a delivery in order before reading, as Part 2 section 2.6.14 splits a message.
  The order of sections (3.2) is not checked: a client reads what a broker sends.
- **PLAIN refuses NUL.** RFC 4616's message joins its three parts with NUL, so a part holding NUL has no encoding.

## Consequences

The client flow (BL-4257) builds performatives with `Amqp1Composite.Create` and reads them by field name; adding a
composite is one catalogue entry. A mistyped field name is a programming error and throws `ArgumentOutOfRangeException`.
