# ADR-1329: The failed-upload-write wait cancels its control read and holds the reply it reads

Status: Accepted. Decided by Claude under Stewart's delegation (BL-5667).

## Context
ADR-1327's 500 ms wait for a `552` left its control-connection read pending when no reply came, so disposing the
session faulted it unobserved and a later reply could be written to `-v`, `--trace` or `-D` after exit 55 was decided.
The wait also ignored the transfer's cancellation token. A non-`552` reply (426, 451) inside the window was read and
shown, where curl 8.22.0's exit 55 path never reads the control connection.

## Decision
The read runs under a token linked to the transfer's, cancelled when the wait ends, and its task is observed. The wait
ends at once when the transfer is cancelled. The reply is read with `FtpControlChannel.ReadReplyHeldAsync`, which shows
nothing until `ReleaseHeldReplyAsync`: only a `552` is released, any other reply is discarded unshown. This follows the
task's reading of curl's source; real curl was not measured for the 426 case because the loopback race is the one
ADR-1327 documents.

## Consequences
No read outlives the wait. A `552` shows exactly as before. Other replies in the window no longer appear on `-v`.
