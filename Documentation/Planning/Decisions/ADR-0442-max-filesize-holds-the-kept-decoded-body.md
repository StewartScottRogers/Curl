# ADR-0442: --max-filesize holds only the kept body, counted after decoding

- Status: Accepted
- Date: 2026-10-08
- Decided by Claude under Stewart's delegation.
- Task: BL-1807 (gap finding GF-0014)

## Context

curl 8.21.0 checks `--max-filesize` against a Content-Length only when the body is kept
(`!k->ignorebody` in `lib/http.c`), so a redirect `-L` follows may announce a body larger
than the limit (upstream test477). It also stops a `--compressed` body whose decoded bytes
grow past the limit with exit 63 (upstream test1618, a decompression bomb). Curl failed
test477 with exit 63 on the redirect, and counted only the encoded bytes, so a bomb was
written out in full.

## Decision

- A discarded body - a redirect being followed, a 401 or 407 before its retry - is not
  held to the limit: `HttpProtocolHandler.DeliveryOf` passes no limit for it.
- With content decoding on, `HttpContentDecoder.MaximumDeliveredSize` holds the decoded
  bytes to the limit: as many decoded bytes as the limit allows are written, then the
  transfer ends with exit 63 and `Exceeded the maximum allowed file size (<limit>) with
  <delivered> bytes`, the message the undecoded limit already gives. The encoded bytes stay
  held to the limit too, as before.

## Consequences

- Writing up to the limit before failing mirrors the undecoded path; the gap run's
  re-measure of test1618 is the check on the exact bytes written.
