# ADR-1237 — A reset met while --limit-rate holds a body of bytes is exit 56

- Status: Accepted
- Date: 2026-11-05
- Task: BL-4874
- Decided by Claude under Stewart's delegation.

## Context

ADR-0730 turns a failed write into exit 56 when the writer had waited on a stream body's upload
source, and keeps any body of bytes at exit 55. AF-0683 measured curl 8.22.0 (Schannel) with
`-d abcdefghij --limit-rate 1` against Record-CurlExchange's default exchange, which answers and
closes: curl ends `curl: (56) Recv failure: Connection was reset` every time, Curl ended
`curl: (55) Send failure: Connection was aborted`. While `--limit-rate` holds the send, curl's
transfer loop polls the socket for reading only, so the peer's reset meets `recv` first, for a
`-d` body exactly as for a `-F` one.

## Decision

`HttpRequestBodyWriter.WriteBytesAtRateAsync` writes each piece that had to wait for the rate
through `WriteAfterWaitAsync`, which applies ADR-0730's probe: when the write fails with exit 55,
the writer reads one byte from the connection, and an `IOException` there gives exit 56 with
`HttpTransferMessages.ReceiveFailure`'s words. A piece written before any wait (the first), a
body of bytes sent without a rate, and a deferred TLS handshake failure keep their exit codes.
This amends ADR-0730's "any body of bytes" to "any body of bytes not held by the rate".

## Consequences

- `-d` and `-F` under `--limit-rate` now fail alike when the server resets, as in curl.
- The probe runs only on a transfer that has already failed, so it costs nothing on success.
