# ADR-1466: FTP data connections resume the control connection's TLS session

- Status: Accepted
- Date: 2026-11-26
- Decided by Claude under Stewart's delegation (BL-6014)

## Context

vsftpd's default `require_ssl_reuse=YES` refuses a data connection that does not resume a
session (STD-220). On Linux, without `-v`, FTPS ran on `SslStreamTlsProvider`, which cannot
resume one connection's session on another port; the hand-built client keyed sessions by port.

## Decision

`ITlsProvider.AuthenticateAsClientResumingSessionOfAsync` names the connection whose session a
handshake resumes. FTP uses it for the `AUTH` upgrade (the control connection names itself) and
for every data connection (naming the control connection). The OpenSSL build of
`SslStreamTlsProvider` runs it on a `HandBuiltTlsProvider` of the same options, which remembers
each handshake and offers its newest TLS 1.3 ticket, or its TLS 1.2 session with only the
versions below TLS 1.3. The Schannel build is unchanged: Windows resumes by itself.

## Consequences

Explicit FTPS on Linux and macOS uses the hand-built client with or without `-v`. Implicit
`ftps://` still handshakes its control connection through the connector, so under the
`SslStream` route its data connections have no session to resume (filed as follow-up work).
`--no-sessionid` resumes nothing.
