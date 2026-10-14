# ADR-0489: A died connection's resend is drawn with one done draw on the progress meter

- Status: Accepted
- Date: 2026-10-13
- Task: BL-2200 (gap finding GF-0078, upstream test675)
- Decided by Claude under Stewart's delegation.

## Context

Upstream test675 fetches two URLs whose server closes the connection after each response.
curl 8.22.0 reuses the first connection for the second URL, finds it dead before any response
(`Connection died, retrying a fresh connect (retry count: 1)`) and sends the request again on a
fresh connection. Its progress meter for the second transfer is the zero line, one more zero
draw, a newline, a new zero line, and the three done lines: `Curl_pgrsDone` makes one forced draw
and writes the newline for the dead attempt, and the resend's meter starts again.

Measured on Windows 2026-10-13 (`Record-CurlExchange.ps1`, BL-2200 Notes): curl 8.22.0 takes the
dead-connection path in every run when the connection was used under about a second ago (6 of 6,
full close and half close); after a two-second gap it connects afresh without a word. curl 8.21.0
asks the connection first and races the FIN (ADR-0467).

Curl reported every resend on a fresh connection, a dead connection's included, as
`ITransferProgress.ReportRequestRetried`, which `TransferProgressRecorder` draws as an
authentication retry: two done draws, a newline, a new zero line (BL-2131).

## Decision

1. `ITransferProgress` gains `ReportRequestResentOnFreshConnection`: a request whose reused
   connection died before its response began is sent again on a fresh connection. Its default
   body reports `ReportRequestRetried`, so no other sink changes.
2. `HttpProtocolHandler` reports it, through `HttpTransferProgress`, for the resend that follows
   an outcome with `DiedBeforeResponse` (a dead pooled connection or an HTTP/2 `REFUSED_STREAM`),
   and `ReportRequestRetried` for every other retry.
3. `TransferProgressRecorder` draws it with one done draw, a newline and a new zero line, keeping
   the transfer's start time as an authentication retry does.
4. `Curl.Core`'s `MaxTimeWatchdog` and `LowSpeedWatchdog` pass it on, so `-m` and `-Y` do not
   turn it back into a retry.

Making Curl's pool reuse a recently used connection without the liveness check, as 8.22.0 does,
so test675 reaches this path, is BL-2203.

## Consequences

A dead connection's resend now draws curl 8.22.0's meter lines. Until BL-2203 lands, Curl reaches
it only when the connection's close arrives after the pool's check, so test675 still measures
`differ`.
