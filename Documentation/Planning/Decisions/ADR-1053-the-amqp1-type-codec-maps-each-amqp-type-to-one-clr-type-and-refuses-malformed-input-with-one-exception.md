# ADR-1053 — The AMQP 1.0 type codec maps each AMQP type to one CLR type and refuses malformed input with one exception

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4258.

## Context

BL-4118 adds an AMQP 1.0 client as the extension `Curl.Extended.Protocol.Amqp1.UnitLibrary` (ADR-0999). Upstream curl has no
AMQP, so OASIS AMQP Version 1.0 (29 October 2012) decides. BL-4258 is its first part: the type system of Part 1,
as pure byte-level code in `Types/`, on which BL-4256 builds the frames and performatives.

## Decision

- **One CLR type per AMQP type.** null is `null`; boolean `bool`; ubyte, ushort, uint, ulong `byte`, `ushort`,
  `uint`, `ulong`; byte, short, int, long `sbyte`, `short`, `int`, `long`; float and double themselves; decimal32,
  decimal64 and decimal128 `Amqp1Decimal`, kept as raw bytes (nothing in a client computes with them); char
  `System.Text.Rune`; timestamp `Amqp1Timestamp`; uuid `Guid` (RFC 4122 byte order); binary `byte[]`; string
  `string`; symbol `Amqp1Symbol`; list any `IReadOnlyList<object?>`; map `Amqp1Map`, its pairs in wire order (keys may
  be `byte[]` or other values with no useful equality); array `Amqp1Array`, carrying its element format code and
  optional element descriptor; a described type `Amqp1Described`. Reading returns the same type whichever encoding
  carried the value (0x41, 0x42 and 0x56 all read as `bool`; uint0, smalluint and uint all as `uint`).
- **Write smallest, read every form.** `Amqp1Writer.WriteValue` picks the smallest encoding (uint0/ulong0 for 0,
  small forms for 0 to 255 or -128 to 127, 8-bit sizes up to 255 bytes, list0 for the empty list); `Amqp1Reader`
  accepts every encoding. An array is written in the element format code its value names, so a caller chooses the
  fixed width; codes with no body (null, true, false, uint0, ulong0, list0) cannot be an array's element constructor
  when writing.
- **One exception both ways.** Every fault - unknown constructor, truncated value, size or count past the bytes left,
  bytes left over inside a compound or after the value, an odd map count, invalid UTF-8, a non-ASCII symbol, a
  boolean byte other than 0x00 or 0x01, a char that is not a Unicode scalar value, nesting past 100 levels - raises
  `Amqp1MalformedEncodingException` (a `FormatException`). A value that cannot be written in the format code asked for
  (a CLR type with no AMQP type, a non-ASCII symbol, 256 in smalluint, a 300-byte body in an 8-bit size) raises the
  same exception, so the client never sends what it would refuse to read.
- **A count is bounded by the bytes left.** A compound's count may not exceed the bytes left in it, so a crafted
  count cannot make the reader loop or allocate without bound. Every element takes at least one byte except in an
  array of a body-less constructor, which no AMQP peer sends; such an array longer than its size is refused.
- **Nesting is bounded.** Lists, maps, arrays and described types nest at most 100 deep, so crafted input raises the
  exception rather than overflowing the stack.

## Consequences

BL-4256 builds composite types (section 1.4: a described list whose trailing nulls may be omitted) on
`Amqp1Described` and lists, and BL-4257's client maps a caught `Amqp1MalformedEncodingException` to its exit code.
No registration yet: BL-4118 adds it.
