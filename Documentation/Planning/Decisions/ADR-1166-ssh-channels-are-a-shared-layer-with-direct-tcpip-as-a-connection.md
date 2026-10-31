# ADR-1166 — SSH channels are a shared layer, with direct-tcpip as a connection

- **Status:** Accepted
- **Date:** 2026-10-30

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4330.

Extends ADR-1165: `Curl.Ssh.UnitLibrary` gains the connection protocol (RFC 4254).

## Context

`--ssh-jump` (BL-4145) is an extension: it may reference `Curl.Ssh.UnitLibrary` but not
`Curl.Protocol.Ssh.UnitLibrary` (ADR-0999). BL-4362 and BL-4329 moved the transport, the host
keys, the private keys and the user authentication there. The jump extension also needs a
channel, and a different one from SFTP's and SCP's: a `direct-tcpip` channel (RFC 4254
section 7.2) whose bytes are the transfer's connection to the target host.

## Decision

1. `Connection` (`SshConnectionMessageNumber`, the channels) and `SshSessionConnection` move to
   `Curl.Ssh.UnitLibrary`, namespaces `Curl.Ssh.Connection` and `Curl.Ssh`.
2. What every channel does - the open with libssh2's 2 MiB window and 32 KiB packets (5.1),
   data within both windows and the peer's maximum packet size (5.2), EOF and close as libssh2
   sends them (5.3), and answering the connection's own messages while it waits - is the
   abstract `SshChannel`. `SshSessionChannel` adds the `session` open and the `subsystem` and
   `exec` requests; `SshDirectTcpipChannel` adds the `direct-tcpip` open (target host and port,
   then originator address and port) and is an `IConnection`. The bytes SFTP and SCP send are
   unchanged.
3. `SshDirectTcpipChannel.IsSecure` is true: its bytes are encrypted as far as the SSH server.
   `RemoteEndPoint` is the target host and port as a `DnsEndPoint`, the address the transfer
   asked for. `FlushAsync` does nothing, since each data packet is written as it is sent.
   `DisposeAsync` closes the channel once, as `CloseAsync` does.
4. A channel logs its open and its requests through its own `SshChannelLog`, over the
   transport's `SshTransportLog`, as `SshAuthenticationLog` does. The open's line names the
   channel type (`session channel opened`, `direct-tcpip channel opened`), so SFTP's and SCP's
   lines are unchanged; `SshDiagnosticLog` keeps only the SFTP lines.
5. No separate "client session" type is added. The steps a session takes - transport,
   algorithm negotiation, key exchange, `ssh-userauth`, host key check, authentication - are
   each already a `Curl.Ssh` type, and `SshProtocolHandler` interleaves curl's `-v` lines and
   state trace between them. The jump extension composes the same types and opens a
   `SshDirectTcpipChannel` on the authenticated transport; wrapping them now would fix a shape
   before its one consumer exists.

## Consequences

- The jump extension can return a `direct-tcpip` channel as the transfer's `IConnection`.
- A channel's tests that need no SFTP or SCP server script live in `Curl.Ssh.UnitTests`;
  `SshSessionChannelTests` stays in `Curl.Protocol.Ssh.UnitTests`, whose `SftpServerScript` it uses.
