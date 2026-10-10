# Curl.Conformance.SshServer.UnitLibrary

The upstream case runner's stand-in for OpenSSH's `sshd`, so the SCP and SFTP cases in
upstream's `tests/data` can run in memory (BL-1899). Read ADR-0456 before changing it.

## Rules

- It references `Curl.Protocol.Ssh.UnitLibrary` and reuses the SSH client's wire code
  (`SshConnectionReader`, `SshPacketReader`, `SshPacketWriter`, `SshIdentificationExchange`,
  and later key exchange and packet protection) through `InternalsVisibleTo`. Never copy
  that code here, and never reference test code such as
  `Curl.Protocol.Ssh.UnitTests\Fakes\InMemorySshServerSession.cs`; port from it instead.
- BCL only, no socket: connections are in-memory pipes (`SshServerDuplexConnection`).
- Held to the `*.UnitLibrary` gates: 100% line and branch coverage, complexity at most 10.

## The transport so far

- `SshServerConnector` is an `IConnector`. Each `ConnectAsync` makes an
  `SshServerDuplexConnection` pair, hands the client its end, and starts a session on the
  server's end; `Sessions` lists them in connection order.
- `SshServerTransport` sends `SSH-2.0-OpenSSH_9.7`, reads the client's identification line
  (skipping lines that do not start with `SSH-`), then reads and writes plain packets with
  the client's own `SshPacketReader` and `SshPacketWriter`.
- Next: key exchange up to `NEWKEYS` and the service request (BL-1935), then
  authentication and the SCP and SFTP subsystems.
