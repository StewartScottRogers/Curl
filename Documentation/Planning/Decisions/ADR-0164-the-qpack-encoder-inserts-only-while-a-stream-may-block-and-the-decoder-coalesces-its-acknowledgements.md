# ADR-0164 — The QPACK encoder inserts only while a stream may block, and the decoder coalesces its acknowledgements

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-729.

## Context

RFC 9204 fixes how a QPACK field section and the encoder and decoder stream instructions
are decoded, but leaves the encoder free to choose each representation, whether to insert,
and what Base to use, and leaves the decoder free to choose when it sends Insert Count
Increments. ADR-0144 records that curl's ngtcp2 build leaves nghttp3's defaults alone, so
it advertises a dynamic table capacity of 0 and 0 blocked streams: a peer encodes with the
static table and literals only, and curl's own encoder never gets a table unless the
server offers one. There is no curl HTTP/3 exchange to measure against without a QUIC
server, so the reference is RFC 9204 appendix B, which `Curl.Http3.UnitTests` replays byte
for byte.

## Decision

`QpackEncoder` (in `Curl.Http3.UnitLibrary`):

1. **Representation order:** a static entry matching name and value; the newest dynamic
   entry matching both; a new entry inserted for the field; a literal naming the static
   entry with the name (the first such entry); a literal naming the newest dynamic entry
   with the name; a literal with a literal name.
2. **Never indexed** fields (`HeaderField.IsNeverIndexed`) skip the indexed forms and the
   dynamic table entirely and are sent as literals with the N bit.
3. **Blocking:** an entry the decoder has not acknowledged is referenced, and a new one is
   inserted for a section, only while the stream may block: it already has a blocking
   section, or fewer streams block than the peer's SETTINGS_QPACK_BLOCKED_STREAMS.
4. **Eviction:** an insert, a duplicate or a capacity change that would evict an entry the
   decoder has not acknowledged, or one that an unacknowledged section (or the section
   being encoded) references, is refused. There is no draining index.
5. **Base** is the Insert Count before the section, so entries inserted for it are
   post-Base; a section with no dynamic reference has Base 0 (prefix `0000`).
6. **Huffman** when the coded string is strictly shorter, as HPACK does (ADR-0148); the
   constructor can turn it off, which is how appendix B's raw literals are reproduced.
7. The dynamic table capacity stays 0 until the caller sets one.

`QpackDecoder`:

1. **Acknowledgements:** a Section Acknowledgement for every decoded section with a
   non-zero Required Insert Count; an Insert Count Increment only when the queued decoder
   stream bytes are taken, and only for insertions no acknowledgement already covers. This
   reproduces appendix B's decoder stream exactly (`84`, `01`, `48`).
2. **Stream Cancellation** is sent for a cancelled stream whenever the advertised table
   capacity is above 0, and never when it is 0 (RFC 9204 section 4.4.2 allows omitting it).
3. **Required Insert Count** must equal the largest referenced absolute index plus one;
   a larger one is `QPACK_DECOMPRESSION_FAILED`, which section 2.2.1 permits.
4. A section beyond the Insert Count blocks its stream while fewer than the advertised
   blocked-streams limit are blocked, and is `QPACK_DECOMPRESSION_FAILED` otherwise.
5. Encoder and decoder stream instructions may arrive split across reads; an incomplete
   one waits for the rest.

## Consequences

- RFC 9204 appendix B.1 to B.5 come out byte for byte from both sides.
- Under curl's own settings (capacity 0, 0 blocked streams) the decoder rejects any
  dynamic reference and the encoder sends static indexes and literals only.
- An incomplete encoder stream instruction is buffered without a size limit; BL-822 bounds
  it. Decoder stream instructions are single integers, rejected past 62 bits, so the
  decoder stream is bounded already.
