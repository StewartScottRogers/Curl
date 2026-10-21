# ADR-0730 — A reset met after waiting on the upload source is exit 56, found by a read after the failed write

- Status: Accepted
- Date: 2026-10-21
- Task: BL-2956
- Decided by Claude under Stewart's delegation.

## Context

AF-0300's reproduction (`--form f=@f.txt --limit-rate 10` against a server that answers after a
pause and closes) ends `curl: (56) Recv failure: Connection was reset` in curl 8.22.0 and
`curl: (55) Send failure: Connection was reset` in Curl. While curl's send is held (a rate limit,
or no upload data ready), its transfer loop polls the socket for reading only and
`Curl_sendrecv` reads before it writes, so the peer's reset is met by `recv`. A write that fails
at full speed stays exit 55 (ADR-0040, BL-174).

Two ways to reach curl's answer:

1. Race a read of the connection against every body read that does not complete at once, as
   `HttpEarlyResponseConnection` races the status line. That leaves a connection read pending
   while the writer goes on writing and the response reader later reads: `SslStream` refuses a
   second concurrent read, and cancelling one in flight is not safe for every `IConnection`
   wrapper (TLS, proxies, rate limiting).
2. Write as now, and only when a write fails after a body read the writer had to wait for, read
   the connection once: a read that fails too is the reset curl's `recv` would have met.

## Decision

Option 2. `HttpRequestBodyWriter` notes whether the last plain read of a `StreamBody` completed
at once. When a write of the piece it gave fails with exit 55 after a read that did not, the
writer reads one byte from the connection: an `IOException` there ends the transfer with exit 56
and `HttpTransferMessages.ReceiveFailure`'s words (`Recv failure: Connection was reset` on
Windows, the socket error's own words elsewhere); bytes or the end of the stream keep exit 55.
A write that fails after a read that completed at once, any body of bytes, and a deferred TLS
handshake failure keep their exit codes.

## Consequences

- No concurrent reads on the connection, so every `IConnection` works unchanged.
- The probe runs only on a transfer that has already failed, so it costs nothing on success.
- A peer that sends a FIN rather than a reset while the upload waits still ends exit 55 once a
  write fails, where curl would see the close on its read; no case measured so far needs that.
