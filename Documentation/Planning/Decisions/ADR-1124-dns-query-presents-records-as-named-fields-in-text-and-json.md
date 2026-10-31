# ADR-1124: The DNS query extension presents records as named fields, in dig-like text and in JSON

- Status: Accepted
- Date: 2026-10-29
- Task: BL-4336
- Decided by Claude under Stewart's delegation

## Context

`Curl.Extension.Protocol.DnsQuery` (split from BL-4150, ADR-0999) sends a DNS query and shows the answer. Upstream curl has
no such feature, so the codec and its output are tested against RFC 1035, RFC 2782, RFC 3597 and RFC 8659, not
against curl. Two choices had no RFC to settle them: how the decoded RDATA is held so both a text and a JSON writer
can show it, and how a decoder tells a compression pointer loop from a legal pointer.

## Decision

- The decoder turns each record's RDATA into an ordered list of named fields (`DnsRecordField`), named after the RFC
  that defines the type: `address`; `nsdname`, `cname`, `ptrdname`; `preference` and `exchange`; `txt`; `mname`,
  `rname`, `serial`, `refresh`, `retry`, `expire`, `minimum`; `priority`, `weight`, `port`, `target`; `flags`, `tag`,
  `value`; and `rdata` holding RFC 3597's `\# length HEX` for any other type. Each field is a number, text, or a list of
  character-strings, and every value is already in master-file presentation format (RFC 1035 section 5.1), with names
  ending in a dot.
- The text writer follows dig: a `;; ->>HEADER<<-` line, a flags and counts line, then each non-empty section, one
  tab-separated line per entry. The JSON writer gives one object (`id`, `opcode`, `status`, `flags`, `question`,
  `answer`, `authority`, `additional`); a record's `data` object holds its fields by name, numbers as JSON numbers and
  character-strings as an array. It uses `Utf8JsonWriter` with relaxed escaping, so no serializer reflection (AOT).
- A compression pointer must point before the start of the labels that hold it (each jump strictly backwards). That
  is what every encoder writes, and it makes any loop fail at once rather than need a jump counter.
- A known type whose RDATA does not fill exactly its RDLENGTH fails the whole message, as RFC 1035 section 4.1.3 fixes
  the RDATA's form per type; a message that ends early fails the same way. Octets after the last counted record are
  ignored.
- The query encoder reads a name in presentation format (`\.` and `\DDD` escapes), and refuses an empty label, a label
  over 63 octets, a name over 255 octets and a character outside ASCII (no IDNA).

## Consequences

BL-4337's scheme handler picks a writer by option and never needs to know a record type. Adding a record type is one
entry in `DnsRecordDataDecoder`'s table.
