# ADR-1434: A CONNECT reply counts in size_header and num_headers for every protocol

- Status: Accepted
- Date: 2026-11-26
- Decided by Claude under Stewart's delegation

## Context

Fitness finding STD-222 (BL-6019): through `-p`, curl 8.22.0 (Schannel) and 8.18.0 (OpenSSL)
add the proxy's CONNECT reply to `%{size_header}` and `%{num_headers}` for FTP, FTPS, SFTP,
SCP, DICT, TELNET and GOPHER. With a proxy whose reply is 62 bytes with one header line, the
reviewer measured `124` and `1` against vsftpd, `124` and `2` against pure-ftpd (whose
greeting holds a line with a colon), and `62` and `1` for SFTP and DICT. Curl printed only the
HTTP handler's figures, since it alone put the reply on its report. The expected values are the
reviewer's measurements; they were not measured again here.

## Decision

For a transfer whose handler reports no CONNECT status, the engine's
`ConnectionEndPointRecorder` puts the recorded replies on the report:

- `%{size_header}` gains the bytes of every reply on a connection that was not reused.
- `%{num_headers}` gains the header lines of the last such reply. curl's tool starts its count
  again at the first header after a blank line, so an earlier tunnel's lines are gone once the
  next one's arrive.
- When there was only one reply and the handler received headers of its own after it, those
  replace the reply's lines, by the same rule.
- When the reply held header lines, the stand-in for "the one header curl counts for a
  successful FTP download" (BL-5403) is dropped: curl reports 1, not 2, against vsftpd.

`TransferReport.RefusedConnectHeaderCount` is renamed `ConnectReplyHeaderCount`, since it now
also counts the lines of a reply that opened a tunnel.

## Consequences

The FTP stand-in is shared as `TransferReport.TransferCompleteStandInHeader` so the engine can
recognise it. A line with a colon in an FTP reply that arrives after a second tunnel opened
would restart curl's count; Curl adds it to the tunnel's lines instead. No measured server
sends one.
