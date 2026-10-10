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
  `curve25519-sha256@libssh.org`), else `diffie-hellman-group14-sha256` for a client with
  no Curve25519 such as curl's libssh2 WinCNG build (BL-1951); the fixed `SshServerHostKey`
  (`ssh-ed25519`), else the fixed `SshServerRsaHostKey` signing as `rsa-sha2-512`,
  `rsa-sha2-256` or `ssh-rsa`, whichever the client agreed; and every cipher and MAC
  `SshPacketProtections` implements, so the client's first implemented cipher and MAC win;
  `none` compression, no strict key exchange. After `NEWKEYS` both directions are
  protected, and `AcceptServiceRequestAsync` accepts `ssh-userauth` (any other service
  throws). A session's task in `Sessions` completes there (BL-1935).
- Next: authentication, then the SCP and SFTP subsystems.

## The host keys and their fingerprints

Both host keys are fixed, so their fingerprints never change. An upstream case that pins
the server's key names the fingerprint of the key its client agrees:

- `SshServerHostKey`, RFC 8032 section 7.1's first Ed25519 key as `ssh-ed25519`, agreed by
  a client that offers `ssh-ed25519` (curl's OpenSSL build on Linux and macOS):
  - `--hostpubmd5 cf07be9d68ae65546da093c36fbd0d82` (`SshServerHostKey.Md5Fingerprint`)
  - `--hostpubsha256 bbXpuKG6zhzdmnxq256TlqzFBzRl2f6OOg722cYNbU8` (`SshServerHostKey.Sha256Fingerprint`)
- `SshServerRsaHostKey`, a 2048-bit `ssh-rsa` key generated once (BL-1951), agreed by a
  client that offers no `ssh-ed25519` (curl's WinCNG build on Windows):
  - `--hostpubmd5 2948c3aaadd13b5fc3053eb5f02ff41d` (`SshServerRsaHostKey.Md5Fingerprint`)
  - `--hostpubsha256 oKu2ijiKRpAnWn3uWXJlDBMslPHR6h9ZZOV89/I8n2o` (`SshServerRsaHostKey.Sha256Fingerprint`)

Upstream's cases compute their key's fingerprints from the key its own `sshd` generates;
the case runner does not reach this server yet, so nothing substitutes these for them. When
the SCP and SFTP cases are wired in, the runner is to supply the agreed key's two values
wherever a case asks for the server's MD5 or SHA-256 host key fingerprint.

## The client key

`SshServerRsaClientKey` is a fixed 2048-bit RSA key pair for the account's public-key
authentication on Windows, where curl's WinCNG build reads no Ed25519 or ECDSA private key
(upstream's `sshserver.pl` generates RSA client keys too): `PrivateKeyPem` is the PKCS #1
file a case passes as `--key`, `PublicKeyLine` the `ssh-rsa` line for `--pubkey` and the
account's authorized key. Authentication (BL-1916) is to accept it.
