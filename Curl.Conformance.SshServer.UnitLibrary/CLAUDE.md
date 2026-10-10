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
- `SshServerUserAuthentication` then answers `ssh-userauth` requests for the one
  `SshServerClientAccount` (user `curltest`): `none` and every other method fail naming
  `publickey,password`; `password` succeeds with `curltest-password`; `publickey` answers
  `PK_OK` for the account's `ssh-ed25519` key or its RSA twin `SshServerRsaClientKey` (as
  `ssh-rsa`, `rsa-sha2-256` or `rsa-sha2-512`) and `SUCCESS` for a valid signature over
  the session identifier and request (BL-1953). Any other user fails.
- `SshServerSessionChannel` then confirms the client's `session` channel (window 2 MiB,
  packets of 32768), refuses every request that wants a reply until an `exec` or
  `subsystem` request, which it accepts and keeps as `ProcessRequest` and `Process`;
  `ReadDataAsync` and `WriteDataAsync` carry data with window accounting both ways, and
  `CloseAsync` sends `exit-status`, `EOF` and `CLOSE`. `SshServerConnector.Channels` lists
  each connection's channel, completing once the process is started.
- `SshServerConnector.Processes` then runs `scp` on each channel whose `exec` command
  `SshServerScpCommand` reads as `scp` with `-f` or `-t` and a path, unquoted as a shell
  would (BL-1917), and SFTP on each channel whose `subsystem` is `sftp` (BL-1918); one
  channel after another, as curl reuses a connection
  (`SshServerSessionChannel.OpenAsync` skips the earlier channel's leftover messages). Any
  other command's or subsystem's channel is left to whoever holds it from `Channels`.
- `SshServerScpProcess` is OpenSSH's `scp` on the real files the path names (a leading
  slash before a Windows drive is dropped). Source (`-f`, `-pf`): after the client's zero
  byte, `T<mtime> 0 <atime> 0`, `C0644 <size> <name>` (always mode 0644: Windows files
  carry no Unix mode), each acknowledged, then the bytes and a zero byte. Sink (`-t`): a
  zero byte, then `T`, `D` (makes the directory and enters it) and `E` (leaves it) lines
  acknowledged, and each `C` line's file written into the path, or into the current
  directory under its name when the path is a directory. A missing file or folder is
  scp's error line, `\x01scp: <path>: No such file or directory`, and exit status 1;
  any other line is a protocol error. `SshServerChannelInput` reads the channel's bytes a
  byte, line or block at a time.
- `SshServerSftpProcess` is OpenSSH's `sftp-server`, SFTP version 3, on the same real files
  (paths mapped as scp's are). `INIT` gets `VERSION 3` with no extensions; `OPEN`, `CLOSE`,
  `READ`, `WRITE`, `STAT`, `LSTAT`, `FSTAT`, `SETSTAT`, `FSETSTAT`, `OPENDIR`, `READDIR`
  (`.`, `..`, then the names in ordinal order with `ls -l` long names, all in one reply,
  then end of file), `REMOVE`, `MKDIR`, `RMDIR`, `REALPATH` (`.` is the current directory,
  given with forward slashes and a slash before a drive) and `RENAME` are answered in
  arrival order; anything else is `SSH_FX_OP_UNSUPPORTED`. Every file is reported as mode
  0644 and every directory as 0755, owner and group 0, and `SETSTAT` changes nothing,
  since Windows files carry no Unix mode. A missing path is `SSH_FX_NO_SUCH_FILE`, a refused
  one `SSH_FX_PERMISSION_DENIED`, and anything else (an existing target, a full directory,
  an unknown handle) `SSH_FX_FAILURE`, as `sftp-server` maps `errno` for version 3. The
  process ends at the client's `EOF` with exit status 0.

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

Upstream's cases compute their key's fingerprints from the key its own `sshd` generates as
`%SSHSRVMD5` and `%SSHSRVSHA256`; the case runner gives those variables the agreed key's two
values (BL-1954): the RSA key's on Windows, the Ed25519 key's elsewhere, chosen by
`UpstreamConformanceTests`, which `InternalsVisibleTo` lets read these internal keys.

## The client key

`SshServerRsaClientKey` is a fixed 2048-bit RSA key pair for the account's public-key
authentication on Windows, where curl's WinCNG build reads no Ed25519 or ECDSA private key
(upstream's `sshserver.pl` generates RSA client keys too): `PrivateKeyPem` is the PKCS #1
file a case passes as `--key`, `PublicKeyLine` the `ssh-rsa` line for `--pubkey` and the
account's authorized key. Authentication accepts it beside the Ed25519 key, whose
`openssh-key-v1` and `authorized_keys` files `SshServerClientAccount.CreatePrivateKeyFile`
and `CreatePublicKeyFile` write (BL-1953).
