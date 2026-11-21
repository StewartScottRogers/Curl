# ADR-1326: A failed FTP upload write waits briefly for the server's 552 reply

Status: Accepted. Decided by Claude under Stewart's delegation (BL-5663).

## Context
A server that refuses an upload mid-transfer sends `552` on the control connection and closes the data connection.
Measured on Windows against a loopback server that does exactly that (BL-5663): curl 8.22.0 exits 70 in 19 of 20
runs and 55 in one for a 200 KB upload, and 55 in 20 of 20 for 1 MB and 50 MB uploads, where the server closes
before the client has sent everything. The port exited 55 in 19 of 20 runs for the 200 KB upload and 70 in one.

## Decision
When a write to the data connection fails, `FtpSession` gives the control connection 500 ms to show a reply. A `552`
is exit 70 (after `QUIT`, as for a `552` at the end of the transfer); anything else, or no reply, is exit 55 as
before. After the change the port gave 70 in 20 of 20 runs of the 200 KB case.

## Consequences
The exit code no longer depends on whether the failed write or the reply is seen first. A failed write with a quiet
server now takes up to 500 ms longer to report exit 55. Curl's own 55-in-20 run is a race on its side that the port
does not copy.
