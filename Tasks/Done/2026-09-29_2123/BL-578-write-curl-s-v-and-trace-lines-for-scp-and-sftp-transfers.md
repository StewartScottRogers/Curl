---
id: BL-578
title: Write curl's -v and --trace lines for scp and sftp transfers
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-576]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests, Curl.Output.UnitLibrary, Curl.Output.UnitTests, Curl.Console.UnitTests, Documentation/Planning/Decisions/ADR-0262-scp-and-sftp-verbose-lines-follow-libssh2-c-and-the-measured-order.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-578 — Write curl's -v and --trace lines for scp and sftp transfers

## Goal

`-v` on an `sftp://` or `scp://` transfer writes the lines curl 8.21.0 writes (connect lines, the SSH fingerprint and host-key lines, the authentication-method lines, the transfer and closing lines), byte for byte apart from values that vary, and `--trace` writes what curl's trace writes for SSH.

## Context

- Conformance audit 2026-09-28, row 35. Events: `ITransferEvents` (ADR-0046); formatting: `Curl.Output.UnitLibrary/VerboseTransferEventWriter.cs`, `TraceTransferEventWriter.cs`.
- **BCL first.** Anything the BCL lacks is hand-built in its own library (standing rule, root `CLAUDE.md`, "Decisions", 2026-09-28): never a package, never a task blocked for a missing primitive.
- Measure with the reference curl against a local OpenSSH server through `Record-CurlExchange.ps1 -NoServer`: `-v` for an SFTP download with password auth, with public-key auth, an SCP download, a host-key mismatch, and `--trace-ascii -` for the SFTP download.

## Acceptance criteria

- [x] Measured first as above; stderr and trace output copied into Notes, with varying parts (fingerprints, ports, times) marked.
- [x] `Curl.Console.UnitTests` pin the measured `-v` stderr for each case with fixed test keys, normalised as existing `-v` tests do.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- **Measurement setup.** The reference curl (8.21.0, libssh2 1.11.1 WinCNG, Schannel) ran
  through `Record-CurlExchange.ps1 -NoServer` with `HOME` an empty directory, against
  BL-569's unpacked OpenSSH 10.2 in WSL as an unprivileged `sshd` on port 2278
  (`PasswordAuthentication yes`). An unprivileged `sshd` cannot check a password, so the
  password case was measured failing; the success line (`SSH: initialized password
  authentication`) and `keyboard-interactive`'s are read from curl 8.21.0's
  `lib/vssh/libssh2.c`, which every measured line matched. Varying parts: `<port>` (the
  client port), `<k>` (key file paths), `<key>` (base64 host keys).
- **Measured `-v`, SFTP with a key** (`-v -sS -k --key <k> --pubkey <k>.pub -u stewart_rogers:`), exit 0:
  ```
  *   Trying 127.0.0.1:2278...
  * Established connection to 127.0.0.1 (127.0.0.1 port 2278) from 127.0.0.1 port <port>
  * SSH: libssh2 cryptography backend: WinCNG
  * SSH: user 'stewart_rogers'
  * SSH: no knownhosts file configured
  * SSH: host offers authentication via: publickey,password
  * SSH: trying public key file '<k>.pub'
  * SSH: trying private key file '<k>'
  * SSH: authenticated via publickey
  * SSH: authentication complete
  { [11 bytes data]
  * Connection #0 to host 127.0.0.1:2278 left intact
  ```
  Without `--pubkey` the `trying public key file` line is absent.
- **Measured `-v`, SFTP with a password** (`-v -sS -k -u stewart_rogers:secret`, no key in
  `HOME`), exit 67: the same to `host offers`, then `* SSH: trying private key file ''`,
  `* SSH: publickey authentication denied: Reason unknown (-1)`, `* SSH: trying publickey
  authentication via agent`, `* SSH: failure connecting to agent`, `* Authentication
  failure`, `* closing connection #0`, `curl: (67) Authentication failure` (the refused
  password wrote no line). Other denials: unauthorized key `Username/PublicKey combination
  invalid`; `--pubkey` right and `--key` missing `Callback returned error`; `--key` another
  key `Invalid signature for supplied public key, or bad username/public key combination`;
  `--key` missing alone `Reason unknown (-1)`.
- **Measured `-v`, SCP with a key**, exit 0: as SFTP with `* SSH: connection established`
  after `* SSH: authentication complete`. SCP missing file: `* Failed to recv file`, `*
  Connection #0 ... left intact`, `curl: (78) Failed to recv file`; SFTP missing file the
  same with `Could not open remote file for reading: No such file or directory`.
- **Measured `-v`, host-key mismatch** (`--knownhosts` with `[127.0.0.1]:2278 ssh-rsa
  <other key>`), exit 60, after the user line:
  ```
  * SSH: found host '127.0.0.1' in '<known_hosts>'
  * SSH: set 'rsa-sha2-256,rsa-sha2-512,ssh-rsa' as hostkey type
  * SSH: host check 1, key: <other key>
  * SSH: knownhost check failed
  * closing connection #0
  curl: (60) SSL peer certificate or SSH remote key was not OK
  ```
  followed by curl's usual `More details here` paragraph. With the right key: `host check 0,
  key: <key>`, `SSH: knownhost entry matches host key`, then the authentication lines.
- **Measured `--trace-ascii -`, SFTP with a key**, on standard output: the same `*` lines,
  then `<= Recv data, 11 bytes (0xb)`, `0000: hello sftp.`, the file's bytes, and `*
  Connection #0 to host 127.0.0.1:2278 left intact`. An SFTP directory listing showed one
  `{ [71 bytes data]` under `-v` (the tool shows only the first block).
- **Design (ADR-0262).** `SshInfoLines` words every line; `SshProtocolHandler` writes the
  backend (`SshAlgorithmPreferences.CryptographyBackend`: `WinCNG`/`OpenSSL`) and user after
  the connect, then narrows the host keys (moved after the connect, as curl reads known hosts
  in libssh2's session start), `SshHostKeyChecker` writes the known-hosts and fingerprint
  lines, `SshUserAuthentication` the method lines (the agent always fails), and
  `ReceivedDataReportingStream` reports each output write. A failure before the transfer, or
  the SFTP subsystem failing (`SshTransferException.EndsConnection`), ends with `closing
  connection #0`; a transfer outcome with `left intact`. Messages curl returns without
  `failf` (`IsVerboseLine` false: `Login denied`, `Error in the SSH layer`, the 60
  known-hosts refusal) are not written as lines. `KnownHostsFile` gained `ReadFailed` and
  `Lookup` (the entry's key for `host check`).
- `Curl.Output.UnitLibrary` needed no change: its writers already format `ReportInfo` and
  `ReportDataReceived` as curl does. Checked against the real `sshd` with the built
  `Curl.Console`: every case above matched byte for byte.
- **Tests.** `Curl.Console.UnitTests/CurlCompositionSshVerboseTests` pins the five cases
  (SFTP password, SFTP key, SCP key, host-key mismatch, SFTP `--trace-ascii -`) against
  `InMemorySshServer` with `TestUserKeys` (made visible to the console tests); the password
  case gives `--key` a missing file so the result does not depend on the machine's `HOME`.
  `Curl.Protocol.Ssh.UnitTests` pins every line and branch: 1096 tests, and the SSH library
  at 100% line, 100% branch, 0 failing members. Fast tests: 0 failures.
- **Follow-up:** BL-988 (sent data for uploads; the unmeasured password,
  `keyboard-interactive` and short-file cases).

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. -v and --trace-ascii on scp:// and sftp:// write curl 8.21.0's SSH session, host-key, authentication, data and closing lines as measured
