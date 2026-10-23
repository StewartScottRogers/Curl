# ADR-0784: The differential responder compresses with the BCL and pins the framing, not the deflate bytes

- Status: Accepted
- Date: 2026-10-23
- Task: BL-3246
- Decided by Claude under Stewart's delegation.

## Context

server.py's `/gzip`, `/deflate`, `/rawdeflate`, `/multienc`, `/gziptrail`, `/gziptrunc` and
`/te-gzip-chunked` send bodies made by Python's zlib (`gz()` at level 9, `zlib.compress` at the
default level, a raw deflate at level 9). .NET's bundled zlib-ng does not produce the same deflate
bytes, and Python may not be run to capture them.

## Decision

`ServerPyCompression` makes the bodies with `GZipStream`, `ZLibStream` and `DeflateStream`:
`SmallestSize` where server.py uses level 9, `Optimal` (zlib's default level 6, header `78 9c`)
where it uses `zlib.compress`. The gzip header's extra-flags and OS bytes are set to `02 03`,
which is what Python's zlib writes at level 9 on any platform (zlib-ng would write the host's OS
byte). The tests pin the head bytes, the gzip and zlib headers, the framing (trailing garbage,
truncation by the 8-byte trailer, the chunk size line) and the decompressed content, not the
deflate bytes.

## Why

The differential runner compares curl with Curl against the same responder, so both clients see
the same body whatever compressor made it. Only the framing changes how a client behaves. Fixed
byte arrays would need a way to measure Python's output, and none is allowed. Setting the OS byte
keeps the response the same on Windows, Linux and macOS.
