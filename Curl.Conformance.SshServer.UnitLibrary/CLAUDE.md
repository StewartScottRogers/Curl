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
- Each session then runs `ExchangeKeysAsync`: `curve25519-sha256` (or
  `curve25519-sha256@libssh.org`), the fixed `SshServerHostKey`, and every cipher and MAC
  `SshPacketProtections` implements, so the client's first implemented cipher and MAC win;
  `none` compression, no strict key exchange. After `NEWKEYS` both directions are
  protected, and `AcceptServiceRequestAsync` accepts `ssh-userauth` (any other service
  throws). A session's task in `Sessions` completes there (BL-1935).
- Next: authentication, then the SCP and SFTP subsystems.

## The host key and its fingerprints

`SshServerHostKey` is RFC 8032 section 7.1's first Ed25519 key as `ssh-ed25519`, so its
fingerprints never change. An upstream case that pins the server's key names them:

- `--hostpubmd5 cf07be9d68ae65546da093c36fbd0d82` (`SshServerHostKey.Md5Fingerprint`)
- `--hostpubsha256 bbXpuKG6zhzdmnxq256TlqzFBzRl2f6OOg722cYNbU8` (`SshServerHostKey.Sha256Fingerprint`)

The case runner substitutes these for the fingerprints of upstream's own test key.
