# ADR-0888: A telnet session sends its upload from the receive loop, after each read's replies

- Status: Accepted
- Date: 2026-10-26
- Task: BL-3536
- Decided by Claude under Stewart's delegation.

## Context

CGR-007 of the third-party gap report (2026-10-25): `TelnetProtocolHandler` sent a `-T`
upload from a task of its own, beside the loop that reads the server and sends negotiation
replies, sharing a lock taken once per write. The upload therefore went out before the
replies to the server's opening negotiation, and could take the lock between the three
writes of `IAC SB NAWS`, the size and `IAC SE`, putting upload bytes inside the
subnegotiation. curl 8.22.0's `telnet_do` runs one loop: it waits for the socket (100 ms
at most while an upload is being read), handles what arrived and sends its replies, and
only then reads and sends the next buffer of the upload.

## Decision

The handler runs curl's single loop. Each turn waits for the server's next read, or with an
upload still going at most 100 ms for it; handles a completed read and sends every reply it
calls for; then sends the upload's next buffer if its read has completed. The upload is read
without the loop ever waiting on it (`TelnetUploadReader`), so an upload that never yields
leaves the session receiving, and a failed upload read ends the upload, as before. When the
server closes, whatever of the upload is already read is still sent, as curl 8.21.0 sent
standard input to a telnet server that closed at once (BL-316).

## Consequences

Measured on Windows against a loopback server playing the report's script (BL-3536 Notes):
for a 60-byte and a 410 KB upload, Curl's bytes on the wire, exit code and stderr equal
curl 8.22.0's. No write is ever concurrent with another, so the send lock is gone.
