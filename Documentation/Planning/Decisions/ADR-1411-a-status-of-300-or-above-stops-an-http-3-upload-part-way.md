# ADR-1411: A status of 300 or above stops an HTTP/3 upload part way

- Status: Accepted
- Date: 2026-11-25
- Task: BL-5979
- Decided by Claude under Stewart's delegation.
- Amends ADR-1409, which left HTTP/3 unchanged.

## Context

ADR-1409 stops an HTTP/2 upload at a status of 300 or above that arrives after the wait for
`100 Continue` ran out. ADR-1403 has HTTP/3 follow HTTP/2. No HTTP/3 measurement was made:
the reference Windows curl has no HTTP/3, so this follows the HTTP/2 measurement.

## Decision

- Once the wait has run out, `Http3StreamConnection` is told to stop the upload at a status
  of 300 or above. The body writer then sends no further piece, writes ADR-1409's two stop
  lines, and the request stream is not ended with a FIN.
- Once the response is read, the stream is cancelled as a timed-out one is (ADR-1410):
  `H3_REQUEST_CANCELLED` and a QPACK Stream Cancellation. The connection is left intact.
  The same cancels a stream whose body a status left unsent during the wait.
- A QUIC stream write buffers and returns at once, so there is no write waiting for window to
  cut, unlike HTTP/2: a piece already handed to the stream counts as sent.
