---
id: BL-1166
title: Write the --trace-config ssh lines from the SSH protocol
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1166 — Write the --trace-config ssh lines from the SSH protocol

## Goal

Under `-v --trace-config ssh` (and `protocol`, `all`) Curl writes the `* [SSH] ...` lines curl 8.21.0 writes for an SFTP and an SCP transfer, from its SSH library.

## Context

- Split from BL-1104 (ADR-0318). Follow BL-1102's pattern: `Curl.Console` decides whether the component is on (`ssh`, `protocol` or `all`) and hands the SSH handler an `ITransferEvents` sink.
- Not measured yet: the recorder has no SSH server. Measure first with `Record-CurlExchange.ps1 -NoServer` against a local sshd (Windows' OpenSSH Server, or sshd on Linux/macOS for the OpenSSL build), and record the lines in Notes. The reference build is libssh2 1.11.1; its `[SSH]` lines are state transitions of curl's `vssh` state machine.

## Acceptance criteria

- [x] The `[SSH]` lines of an SFTP download and an SCP download under `-v --trace-config ssh` are measured and recorded in Notes.
- [x] Tests pin them; `protocol` and `all` write the same; `-v` alone, another component and `ssh` without `-v` write none.
- [x] `--ai-help` still describes `--trace-config` correctly.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

- Measured 2026-10-02 with real curl 8.21.0 (Schannel, libssh2 1.11.1, WinCNG) against
  OpenSSH 10.2: `sshd` in WSL Ubuntu (openssh-server installed with `wsl -u root`), run as
  `sshd -D -e -p 2222` with `AuthorizedKeysFile`, `PasswordAuthentication=no`, and
  `KexAlgorithms=+diffie-hellman-group14-sha256,...`, `HostKeyAlgorithms=+ssh-rsa,...`,
  `Ciphers=+aes128-ctr,...`, `MACs=+hmac-sha2-256,...` (WinCNG's libssh2 failed the default
  key exchange with `-5`, and could not use an Ed25519 key: `Callback returned error`).
  Command: `curl -s -o NUL -v --trace-config ssh --insecure --key r --pubkey r.pub
  sftp://user@127.0.0.1:2222/home/user/hello.txt` (and `scp://`), an 11-byte file, RSA PEM key.
- `[STATE] statemachine() -> 0, block=1` and `[SSH] pollset, flags=1` lines appear wherever
  libssh2 would block; their count varied between identical runs (1 to 25 in `SSH_SFTP_CLOSE`).
  Decided (ADR-0372): not written. Below they are removed.
- SFTP, after `* SSH: user '...'`:
  `[SSH_STOP] -> [SSH_INIT]`, `[SSH_INIT] -> [SSH_S_STARTUP]`, `[SSH_S_STARTUP] -> [SSH_HOSTKEY]`,
  `no host key checksum given, checking knownhosts`, (`SSH: no knownhosts file configured`),
  `[SSH_HOSTKEY] -> [SSH_AUTHLIST]`, (`SSH: host offers authentication via: publickey`),
  `[SSH_AUTHLIST] -> [SSH_AUTH_PKEY_INIT]`, (trying public/private key file lines),
  `[SSH_AUTH_PKEY_INIT] -> [SSH_AUTH_PKEY]`, (`SSH: authenticated via publickey`),
  `[SSH_AUTH_PKEY] -> [SSH_AUTH_DONE]`, (`SSH: authentication complete`),
  `[SSH_AUTH_DONE] -> [SSH_SFTP_INIT]`, `[SSH_SFTP_INIT] -> [SSH_SFTP_REALPATH]`,
  `[SSH_SFTP_REALPATH] -> [SSH_STOP]`, `CONNECT phase done`, `[SSH_STOP] statemachine() -> 0, block=0`,
  `DO phase starts`, `[SSH_STOP] -> [SSH_SFTP_QUOTE_INIT]`, `[SSH_SFTP_QUOTE_INIT] -> [SSH_SFTP_GETINFO]`,
  `[SSH_SFTP_GETINFO] -> [SSH_SFTP_TRANS_INIT]`, `[SSH_SFTP_TRANS_INIT] -> [SSH_SFTP_DOWNLOAD_INIT]`,
  `[SSH_SFTP_DOWNLOAD_INIT] -> [SSH_SFTP_DOWNLOAD_STAT]`, `[SSH_SFTP_DOWNLOAD_STAT] -> [SSH_STOP]`,
  `[SSH_STOP] statemachine() -> 0, block=0`, `DO phase is complete`, (`{ [11 bytes data]`),
  `[SSH_STOP] -> [SSH_SFTP_CLOSE]`, `SFTP DONE done`, `[SSH_SFTP_CLOSE] -> [SSH_STOP]`,
  `[SSH_STOP] statemachine() -> 0, block=0`, (`Connection #0 ... left intact`). Each line is
  `* [SSH] ` and the text.
- SCP: the same to `SSH: authentication complete`, then (`SSH: connection established`),
  `[SSH_AUTH_DONE] -> [SSH_STOP]`, `[SSH_STOP] statemachine() -> 0, block=0`, `DO phase starts`,
  `[SSH_STOP] -> [SSH_SCP_TRANS_INIT]`, `[SSH_SCP_TRANS_INIT] -> [SSH_SCP_DOWNLOAD_INIT]`,
  `[SSH_SCP_DOWNLOAD_INIT] -> [SSH_STOP]`, `[SSH_STOP] statemachine() -> 0, block=0`,
  `DO phase is complete`, (data), `[SSH_STOP] -> [SSH_SCP_DONE]`, `[SSH_SCP_DONE] -> [SSH_SCP_CHANNEL_FREE]`,
  `SCP DONE phase complete`, `[SSH_SCP_CHANNEL_FREE] -> [SSH_STOP]`, `[SSH_STOP] statemachine() -> 0, block=0`.
  SCP writes no `CONNECT phase done`.
- A denied key (Ed25519 under WinCNG) went on `[SSH_AUTH_PKEY] -> [SSH_AUTH_PASS_INIT]`,
  `-> [SSH_AUTH_HOST_INIT]`, `-> [SSH_AUTH_AGENT_INIT]`, `-> [SSH_AUTH_KEY_INIT]`, `-> [SSH_AUTH_DONE]`,
  `Authentication failure`, `[SSH_AUTH_DONE] -> [SSH_SESSION_FREE]`, `[SSH_SESSION_FREE] statemachine() -> 67, block=0`.
  Only the first step is traced here; the rest is BL-1204's.
- `--trace-config protocol` and `all` wrote the `[SSH]` lines too; `-v` alone and
  `--trace-config ssh` without `-v` wrote none.
- Curl's built `curl.exe` against the same server wrote stderr identical to real curl's with the
  `block=1`/`pollset` lines removed, for both schemes (only the source port differed).
- Implemented: `SshStateTrace` (Ssh library), `SshProtocolHandler.TracesStateMachine`,
  `CurlComposition.TracesSsh` and `CurlTransports.TracesSsh`. ADR-0372. Unmeasured paths
  (uploads, listings, `-Q`, password, agent, keyboard-interactive, fingerprints, failures) filed
  as BL-1204.
- `--ai-help all` describes `--trace-config` generically (comma-separated components, `all`,
  `ids`, `time`); still correct, no change needed.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. -v --trace-config ssh (and protocol, all) writes curl's [SSH] state lines for a publickey login and SFTP and SCP downloads
