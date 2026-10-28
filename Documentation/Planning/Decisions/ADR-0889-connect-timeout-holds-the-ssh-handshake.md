# ADR-0889: --connect-timeout holds the SSH handshake and authentication

- Status: Accepted
- Date: 2026-10-26
- Decided by Claude under Stewart's delegation (BL-3539, CGR-010)

## Context

`SshProtocolHandler` passed `--connect-timeout` only to the TCP connect. A server that
accepted the connection and then sent nothing, or only an identification line, held the
transfer until it closed the connection. curl 8.22.0 (libssh2 1.11.1, Schannel build on
Windows) holds every state before `DO` to the connect timeout. Measured 2026-10-26 with
`Record-CurlExchange.ps1` against a server that stays silent, and one that sends
`SSH-1.5-old` and then nothing: it exits 28 with `Operation timed out after N
milliseconds with 0 bytes received`, N counted from the start of the request. It sends a
`DISCONNECT` (`Shutdown`) only once the server's identification has arrived.

## Decision

`SshConnectPhaseLimit` starts when `ExecuteAsync` starts, with `--connect-timeout`, or
curl's 300-second default when the option is not given or is 0, on the context's
`TimeProvider`. Its token cancels the identification exchange, the key exchange, the
`ssh-userauth` request, the host-key check and the authentication. The SFTP or SCP start
and the transfer itself keep the transfer's own token. When the limit passes, the handler
returns exit 28 with curl's message, writes it as a `-v` line and then the
`Closing connection` line. Once `SshTransport.HasServerIdentificationArrived` is true it
sends `DISCONNECT` first.

## Consequences

The three CGR-010 repros now match curl's exit code, stderr, stdout and wire bytes.
Holding the SFTP subsystem start to the limit as well, as libcurl does, is left until a
measurement shows a difference there.
