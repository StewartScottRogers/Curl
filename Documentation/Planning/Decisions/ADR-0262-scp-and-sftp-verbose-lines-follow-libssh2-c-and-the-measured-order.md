# ADR-0262 — `scp` and `sftp` `-v` lines follow `lib/vssh/libssh2.c` and the measured order

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-578.

## Context

`-v` and `--trace` on an `scp://` or `sftp://` transfer wrote only the connector's `Trying`
and `Established connection` lines: `Curl.Protocol.Ssh.UnitLibrary` reported nothing through
`ITransferEvents` (ADR-0046). curl 8.21.0 writes a line for every step of libssh2's session,
the received data, and how the connection ends.

The Windows reference build (curl 8.21.0, libssh2 1.11.1 on WinCNG, Schannel) ran through
`Record-CurlExchange.ps1 -NoServer` with `HOME` an empty directory, against BL-569's unpacked
OpenSSH 10.2 in WSL run as an unprivileged `sshd` (port 2278, `PasswordAuthentication yes`).
An unprivileged `sshd` cannot check a password, so a right password and `keyboard-interactive`
could not succeed; their lines are read from curl 8.21.0's `lib/vssh/libssh2.c`, whose every
other line matched the measurement. Measured `-v` stderr, from the line after `Established
connection` (fingerprint keys shortened to `<key>`; paths are the recorder's):

| Case | Lines | Exit |
| --- | --- | ---: |
| `sftp`, `-k --key k --pubkey k.pub` | `SSH: libssh2 cryptography backend: WinCNG`, `SSH: user 'stewart_rogers'`, `SSH: no knownhosts file configured`, `SSH: host offers authentication via: publickey,password`, `SSH: trying public key file 'k.pub'`, `SSH: trying private key file 'k'`, `SSH: authenticated via publickey`, `SSH: authentication complete`, `{ [11 bytes data]`, `Connection #0 to host 127.0.0.1:2278 left intact` | 0 |
| the same, `--key` alone | no `trying public key file` line | 0 |
| `scp`, the same keys | as `sftp`, with `SSH: connection established` after `authentication complete` | 0 |
| `sftp`, a password, no key in `HOME` | ... `SSH: trying private key file ''`, `SSH: publickey authentication denied: Reason unknown (-1)`, `SSH: trying publickey authentication via agent`, `SSH: failure connecting to agent`, `Authentication failure`, `closing connection #0` (the password was refused, with no line of its own) | 67 |
| key not authorized | `SSH: publickey authentication denied: Username/PublicKey combination invalid`, then the agent lines | 67 |
| `--pubkey` right, `--key` missing | `... denied: Callback returned error` | 67 |
| `--pubkey` of the authorized key, `--key` another | `... denied: Invalid signature for supplied public key, or bad username/public key combination` | 67 |
| `--key` missing, no `--pubkey` | `... denied: Reason unknown (-1)` | 67 |
| `--knownhosts` with another key | `SSH: found host '127.0.0.1' in '<file>'`, `SSH: set 'rsa-sha2-256,rsa-sha2-512,ssh-rsa' as hostkey type`, `SSH: host check 1, key: <key in the file>`, `SSH: knownhost check failed`, `closing connection #0` | 60 |
| `--knownhosts` with the key | ... `SSH: host check 0, key: <key>`, `SSH: knownhost entry matches host key`, then the authentication lines | 0 |
| `sftp` missing file | ... `SSH: authentication complete`, `Could not open remote file for reading: No such file or directory`, `Connection #0 ... left intact` | 78 |
| `scp` missing file | ... `SSH: connection established`, `Failed to recv file`, `Connection #0 ... left intact` | 78 |
| `sftp` directory | one `{ [71 bytes data]` (the first line's write; the tool shows only the first) | 0 |
| a failed handshake | `Failure establishing ssh session: -8, Unable to exchange encryption keys`, `closing connection #0` | 2 |

`--trace-ascii -` writes the same lines, and `<= Recv data, 11 bytes (0xb)` with its dump for
each block received, before the block itself on standard output.

## Decision

1. **The handler reports each line through `ITransferEvents.ReportInfo`**, worded as
   `SshInfoLines` holds them, in curl's order: the backend and the user after the connect; the
   known-hosts file's reading and narrowing (`failed to read known hosts from`, `found host`
   with `set '<names>' as hostkey type`, or `did not find host`) before the handshake; the
   fingerprint or known-hosts check after it (`SHA256`/`MD5 public key`, `fingerprint`,
   `checksum match`; or `no knownhosts file configured`; or `host check <n>, key: <key>` and
   whether it matched); then the method list and each method's lines, `authentication
   complete`, and for `scp` `connection established`.
2. **The narrowing moves after the connect.** curl reads the known-hosts file in libssh2's
   session start, after the TCP connect, so an RSA1 or unknown entry now fails with the
   connection open, as curl's `Trying` lines show.
3. **The backend is the preset's.** `SshAlgorithmPreferences.CryptographyBackend` is `WinCNG`
   for `WindowsReference` and `OpenSSL` for `OpenSslReference`, the libssh2 backend of each
   platform's reference build; `Full` names none and writes no line.
4. **The agent always fails.** This client has no SSH agent, and the reference machine had
   none running, so when the list names `publickey` and neither `publickey` nor `password`
   authenticated, the two agent lines are written before `keyboard-interactive`, as measured.
5. **`publickey`'s reason is libssh2's text for the step that failed**: the public key
   unreadable or no signature algorithm the server accepts, `Reason unknown (-1)`; the question
   refused, `Username/PublicKey combination invalid`; the private key unreadable or of another
   type, `Callback returned error`; the signed request refused, `Invalid signature for supplied
   public key, or bad username/public key combination`. A connection lost during the method is
   given the refusal's text too; it was not measured.
6. **How the connection ends.** A failure before the transfer starts, or the `SFTP` subsystem
   failing to start (still curl's connect phase), writes its message as a line (curl's `failf`)
   and then `closing connection #N`. Every transfer outcome, success or failure, writes its
   failure message as a line and then `Connection #N to host <host>:<port> left intact`, as the
   measured missing-file cases do; a short file's `PartialFile` outcome is treated the same,
   unmeasured. A message curl returns without `failf` - `Login denied`, `Error in the SSH
   layer`, the known-hosts refusal's `SSL peer certificate or SSH remote key was not OK` - is
   not written as a line (`SshTransferException.IsVerboseLine`).
7. **Received data is reported by write.** The handler wraps the output in
   `ReceivedDataReportingStream`, which reports each write before passing it on; the console's
   writers already turn that into `{ [N bytes data]` and `<= Recv data`. The block sizes follow
   this client's reads, not curl's 16 KiB buffer, for files larger than one read.

## Consequences

- `Curl.Output.UnitLibrary` needed no change.
- Uploads report no `} [N bytes data]` or `=> Send data`, and the agent is never really
  asked; both are left to follow-up work.
