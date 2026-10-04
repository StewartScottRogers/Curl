# ADR-0414 — A TFTP first reply that switches direction hands the transfer over

- Status: Accepted
- Date: 2026-10-04
- Task: BL-1444
- Decided by Claude under Stewart's delegation.

## Context

curl 8.21.0 reads a TFTP packet's opcode as its state machine's event. Measured 2026-10-04
with `Record-CurlExchange.ps1 -Tftp -TftpNoOack -TftpReply '<step>=PACKET <hex>'` (BL-1444
Notes): opcode 0 or 7 as the first reply notes `Internal error: Unexpected packet` and re-sends
the request; opcode 7 later notes it and acts as a timeout; an ACK as a download's first reply
connects for transmit and sends an empty DATA 1; a DATA packet as an upload's first reply
connects for receive and writes the block to the output. Curl models a download and an upload
as two classes, `TftpDownload` and `TftpUpload`.

## Decision

1. A first reply that switches direction hands the transfer over: the download builds a
   `TftpUpload` of an empty stream, the upload a `TftpDownload`, and calls its `TakeOverAsync`
   with a `TftpHandOver` - the request as the last packet, the retries counted, when the next
   re-send is due, the failure message noted so far, and the reply itself, which the new
   direction answers first. One state machine per direction stays; neither duplicates the
   other's code.
2. Every `failf` curl makes in these paths is a noted failure: the first one noted is the
   message of whatever failure ends the transfer, as `Received too short packet` already was.
   This now covers `Internal error: Unexpected packet` and an upload's
   `tftp_tx: internal error, event: N` (measured: exit 28 with that message when a switched
   download's retries run out).
3. A re-send caused by opcode 0 or 7 counts a retry and does not move the next scheduled
   re-send, as for a datagram under four bytes; curl updates its receive time only on silence.

## Consequences

The direction switch reuses each direction's measured behaviour, so an ACK of a block other
than 0 on a download, or DATA of a block other than 1 on an upload, follows what the new
direction does with it; those cases were not measured.
