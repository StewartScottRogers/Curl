# ADR-0816: `-w` header values keep their raw bytes

- Status: Accepted
- Date: 2026-10-26
- Task: BL-3550
- Decided by Claude under Stewart's delegation.

## Context

The response head reaches `TransferReport` as Latin-1 strings: one character per byte. The
`-w` renderer wrote every string as UTF-8, so a header byte from 0x80 came out as two bytes, and
a UTF-8 value came out double-encoded. curl 8.22.0 (mingw, Schannel), measured on 2026-10-26 with
`Record-CurlExchange.ps1`, writes `%{content_type}`, `%header{…}`, `%{header_json}` and the
`content_type` member of `%{json}` as the raw header bytes. It writes the template text and the
command-line values (the URL, the file name) as their UTF-8 bytes (third-party gap report
CGR-021).

## Decision

`WriteOutTemplateRenderer.MarkHeaderBytes` carries each header byte from 0x80 to 0xFF as a
private-use character U+F780 to U+F7FF, byte `b` as `U+F700 + b`. The renderer's encoder writes
those characters back as the single bytes they stand for, and everything else as UTF-8. This
keeps a template a single .NET string through rendering, so `%{json}` can hold both kinds of text,
and `IWriteOutVariableSource` stays as it is. `TransferWriteOutVariables` marks
`content_type`, `header_json` and the `%header{…}` values, and matches a `%header{name}` against
the name's UTF-8 bytes. `header_json` lowercases only ASCII letters in header names, as curl does.

A template that itself holds a character from U+F780 to U+F7FF writes it as a raw byte. No
real template holds one, so we accept that cost instead of carrying bytes and text separately
through the renderer.

`%{redirect_url}` is left as it was. curl builds it from the resolved URL, not from the raw
`Location` bytes.

## Consequences

Both CGR-021 repros match curl byte for byte, apart from the values that change from run to
run. `Cgr021Tests` pins the bytes.
