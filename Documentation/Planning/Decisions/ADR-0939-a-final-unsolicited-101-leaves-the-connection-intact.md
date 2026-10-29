# ADR-0939: A final unsolicited 101 whose body runs to the close is reported left intact

- Status: Accepted
- Date: 2026-10-28
- Decision: Decided by Claude under Stewart's delegation (BL-3870)
- Relates to: [ADR-0050](ADR-0050-connections-are-reused-across-requests-and-urls-through-a-pooling-connector.md), [ADR-0324](ADR-0324-an-http-1-0-keep-alive-body-read-to-the-close-is-reported-left-intact.md)

## Context

Since BL-3599 an unsolicited `101` is the final head and its body runs to the close. Measured with
`Record-CurlExchange.ps1` (curl 8.22.0, 2026-10-28), `curl -v` on `HTTP/1.1 101 Switching\r\n\r\nhello` ends with
`Connection #0 to host ... left intact`, and `curl -v URL URL` against it tries to reuse connection 0, finds it dead
(`Recv failure`, `Connection died, retrying a fresh connect`, `shutting down connection #0`) and finishes on
connection #1, which is again left intact. Curl wrote `shutting down connection #N`.

## Decision

Like an HTTP/1.0 keep-alive body that runs to the close (ADR-0324), such an exchange is marked reusable and reported
left intact (`HttpProtocolHandler.EndedAtUnsolicitedSwitchingProtocols`, part of `LeftIntactAfterServerClosed`); the
pool reports it dead before any reuse. An h2c upgrade to HTTP/2 is unchanged.
