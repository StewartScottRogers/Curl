# ADR-0020 — `--compressed` advertises `deflate, gzip, br` until a zstd decoder exists

- **Status:** Accepted
- **Date:** 2026-09-26

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions").

## Context

The Phase 1 HTTP plan (protocol-architect, 2026-09-26, item D4) needs to know what
`--compressed` puts in the `Accept-Encoding` request header before the decoding task
(BL-177) and the option parser (BL-191) can be written.

The reference build (ADR-0018: curl 8.21.0, mingw build) advertises four encodings.
Measured with `curl --compressed` against a local listener, the request it sends is:

```
GET / HTTP/1.1\r\n
Host: 127.0.0.1:18081\r\n
User-Agent: curl/8.21.0\r\n
Accept: */*\r\n
Accept-Encoding: deflate, gzip, br, zstd\r\n
\r\n
```

Curl is base class library only (root `CLAUDE.md`). The BCL decodes three of those four:
`GZipStream` for `gzip`, `ZLibStream` and `DeflateStream` for `deflate`, and
`BrotliStream` for `br`. It has no zstd decoder, and adding a package for one needs
Stewart's approval.

Advertising an encoding Curl cannot decode is worse than not advertising it: a server
that honours the header would answer in zstd, and Curl would have to fail a transfer the
reference build completes.

## Decision

On every platform, `--compressed` sends

```
Accept-Encoding: deflate, gzip, br\r\n
```

and decodes a response in those encodings with `GZipStream`, `ZLibStream`/`DeflateStream`
and `BrotliStream`. The `zstd` token of the measured header is dropped, and only that
token; order and spelling of the other three match the reference.

**Condition for adding `zstd`:** when a hand-written zstd decoder built on the BCL lands
(filed under the Roadmap's "Later / unscheduled"), `--compressed` advertises the measured
header, `deflate, gzip, br, zstd`, and this ADR is superseded.

## Consequences

- Every encoding Curl advertises, it can decode, so no server that honours the header
  can make a `--compressed` transfer fail.
- The request differs from the reference by one token in one header. For a server that
  honours `Accept-Encoding`, the response body Curl writes is byte-identical to the
  reference's output: the server picks one of the three shared encodings instead of zstd,
  and both binaries write the decoded body.
- A test or script that inspects the request header itself (for example `-v` output or a
  server-side echo) sees the difference. That is the whole divergence, and it is known.
- No package is added; native AOT is unaffected.

## Alternatives considered

- **Advertise `deflate, gzip, br, zstd` and fail on a zstd response.** Byte-identical
  request, but a zstd-capable server would answer in zstd and Curl would fail a transfer
  the reference completes: a larger divergence than one header token.
- **Advertise `zstd` and pass the zstd body through undecoded.** Writes compressed bytes
  where the reference writes decoded ones; silently wrong output.
- **Add a zstd package.** Needs Stewart's approval, is a native-AOT trim risk, and breaks
  the base-class-library-only rule for one encoding few servers prefer over `br`.
- **Write the zstd decoder now.** A substantial piece of work (frame format, FSE and
  Huffman entropy decoding) that would hold up Phase 1 HTTP for one header token; kept for
  later instead.
