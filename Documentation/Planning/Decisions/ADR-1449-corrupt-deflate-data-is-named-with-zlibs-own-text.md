# ADR-1449 — Corrupt deflate data is named with zlib's own text

- **Status:** Accepted
- **Date:** 2026-11-26

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-6018.

## Context

Fitness finding STD-208: a corrupt raw deflate body sent as `Content-Encoding: deflate` makes curl
8.22.0 (Schannel) and 8.18.0 (OpenSSL) exit 61 with `Error while processing content unencoding:
invalid distance too far back` and write nothing; Curl gave the generic `Unrecognized or bad HTTP
Content or Transfer-Encoding` and wrote 7 bytes. Measured again here on 2026-11-26 with
`C:\Windows\System32\curl.exe` 8.23.0 (Schannel, zlib 1.3.2) against a loopback server sending a raw
deflate body with garbage after byte 10: exit 61, that text, no output file. curl prints zlib's
`z->msg` for every data error, but the BCL's streams throw `InvalidDataException` without it, and
`HttpContentCodingDecoder` named only faults in a first block's header.

## Decision

1. `HttpDeflateFaultFinder` walks the deflate data the body began with (up to 64 KiB) as zlib's
   inflate does - stored, fixed and dynamic blocks, zlib's checks of each code (inftrees.c's rules for
   over-subscribed and incomplete codes), every literal, length and distance - counting the bytes it
   would decode, and names zlib's text for the first fault. A stream that ends, or runs out, before a
   fault has no text and keeps the generic one. It is written against RFC 1951 and zlib's source; no
   inflater package.
2. Which bytes it walks follows curl: a `deflate` body whose zlib stream fails before anything was
   written is read again as raw deflate from byte 0 (as before); once bytes are written curl stays
   with zlib, so the data after the 2-byte header is walked. A gzip member's data starts after a plain
   10-byte header.
3. A `deflate` body's decoded bytes, zlib-wrapped or raw, are held until the piece of the body they
   came from is decoded, every piece, not just the first: curl writes nothing a failing `inflate`
   call decoded.
4. Four tests that pinned the generic text for in-block faults (`Cgr071Tests`,
   `CorruptDeflateMessageTests`, `CorruptZLibDeflateOutputTests`) now pin zlib's text, which is what
   curl prints whenever zlib sets `z->msg`.

## Consequences

Every data error zlib names inside the first 64 KiB of a deflate or gzip body now gets curl's text;
one further on keeps the generic text.
