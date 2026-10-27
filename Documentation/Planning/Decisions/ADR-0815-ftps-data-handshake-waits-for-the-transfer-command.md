# ADR-0815: The FTPS data connection's TLS handshake waits for the transfer command

- **Status:** Accepted
- **Date:** 2026-10-26
- **Decided by Claude under Stewart's delegation** (BL-3534, third-party gap report CGR-005)

## Context

After an accepted `PROT P`, Curl ran the data connection's TLS handshake as soon as a
passive (`EPSV` or `PASV`) data connection was dialled, before `TYPE`, `SIZE` and `RETR`.
Servers such as vsftpd and proftpd start TLS on the data connection only after the
transfer command, so both sides waited for each other and the transfer hung until killed
or until `-m` ended it with exit 28.

curl 8.22.0 adds its TLS filter to the data connection at once and drives the handshake
without blocking: it sends the ClientHello right after the connect, then sends `TYPE`,
`SIZE` and `RETR` on the control connection, and the handshake completes once the server
answers it. Measured with `Record-CurlExchange.ps1 -Ftp`, whose server starts data TLS only
after `RETR` or `LIST`: real curl exits 0 with the file.

## Decision

`FtpSession` runs a passive data connection's handshake where it already ran an active
one's: in `ReadyDataConnectionAsync`, once the transfer command has been answered. The
`ITlsProvider` seam is one awaited call, so the handshake is not split into curl's
non-blocking steps; starting it concurrently with the control conversation would report
its `-v` lines from a second thread, interleaved unpredictably with the control lines.

## Consequences

- A server that starts data TLS after the transfer command is served, as real curl serves
  it; one that starts it at accept time is served too, as long as it reads the control
  connection meanwhile (every measured server does).
- Under `-v` the data connection's `schannel:` lines follow `150` rather than being spread
  between `Trying` and `150` as curl's step-by-step handshake writes them; `-v` text of the
  Schannel handshake already differed from curl's and stays a separate gap.
- A failed data handshake still ends the transfer with its exit code and no `QUIT`, now
  after `RETR` was sent.
