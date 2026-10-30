# ADR-1056 — The AMQP 0-9-1 codec maps each wire fault to one AMQP reply code

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4259.

## Context

BL-4117 adds an AMQP 0-9-1 client (`amqp://`, `amqps://`) as the extension `Curl.Extension.Amqp0.UnitLibrary`
(ADR-0999). Upstream curl has no AMQP, so the AMQP 0-9-1 specification (2008-11-13) with RabbitMQ's errata decides.
BL-4259 is its wire format: protocol header, frames, data types, field tables, content header and body frames, and the
methods a publish, get and queue-declare client sends or receives. The task asks that every malformed frame or method
raise one named exception carrying the reply code, and the specification leaves open which code fits which fault.

## Decision

- One exception, `Amqp0WireFormatException` (a `FormatException`), carries an `Amqp0ReplyCode`, raised both when
  reading and when about to write bytes the format forbids (as ADR-1044 does for MQTT 5).
- **501 frame-error** for faults of the frame itself: a frame-end other than 0xCE, an unknown frame type, a frame larger
  than the negotiated frame-max (0 meaning no limit), a heartbeat off channel 0 (section 4.2.7), a body longer than its
  content header said, and a peer answering with a protocol header other than `AMQP\x00\x00\x09\x01`.
- **502 syntax-error** for faults in fields: a truncated field, a short string over 255 bytes or not UTF-8, an unknown
  field value tag, a timestamp outside .NET's range, bytes after a method's or content header's last field, a property
  flags continuation bit (the basic class has 14 properties), and a frame-max below 4096 when splitting a body.
- **503 command-invalid** for a class and method id pair the library does not know.
- **505 unexpected-frame** for a frame out of place: a non-method frame where a method is read, or anything but the
  header then body frames of the same channel while content is collected.
- Field tables take RabbitMQ's errata tags (`t b B s u I i l f d D S A T F V x`), the set brokers actually send, not
  the 0-9-1 grammar's conflicting list. Values are made only through typed factories, so an encoder never meets a value
  of the wrong CLR type.

## Consequences

The client flow (BL-4260) turns any `Amqp0WireFormatException` into a connection.close with its reply code, and the
handler (BL-4117) maps it to a curl exit code. The codec never throws an index, cast or decoder exception on bad input.
