---
id: BL-1204
title: Write the --trace-config ssh lines for uploads, listings, -Q commands, other logins and failures
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1166]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1204 — Write the --trace-config ssh lines for uploads, listings, -Q commands, other logins and failures

## Goal

Under `-v --trace-config ssh` Curl writes the `[SSH]` lines curl 8.21.0 writes for an SFTP and SCP upload, an SFTP directory listing, `-Q` commands, password, agent and keyboard-interactive logins, `--hostpubsha256`/`--hostpubmd5` checks and failed transfers.

## Context

- BL-1166 traced only a `publickey` login and SFTP and SCP downloads (ADR-0372); `SshStateTrace` is the writer, and the other paths write only the state changes they share with those.
- Measure as BL-1166's Notes describe: `sshd` in WSL Ubuntu (`wsl -u root`; openssh-server is installed) on port 2222 with `KexAlgorithms`, `HostKeyAlgorithms`, `Ciphers` and `MACs` widened for WinCNG, an RSA PEM key in `AuthorizedKeysFile`, and real `curl.exe -v --trace-config ssh --insecure --key ... --pubkey ...`. Leave out the `block=1` and `pollset` lines (ADR-0372).

## Acceptance criteria

- [x] Each path named in the Goal is measured and its `[SSH]` lines recorded in Notes (the agent only up to its failed connection; success filed as BL-1205).
- [x] `Curl.Protocol.Ssh.UnitTests` pins each against `InMemorySshServer`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

- Measured 2026-10-02 with `/mingw64/bin/curl` 8.21.0 (Schannel, libssh2 1.11.1, WinCNG) against
  OpenSSH in WSL: user `ftuser`/`pw`, `sshd -D -e -f` with port 2222 (publickey, password) and 2223
  (keyboard-interactive only, `UsePAM yes`), algorithms widened as BL-1166's Notes say, RSA PEM key.
  `block=1`/`pollset` removed (ADR-0372). Each line below is `* [SSH] ` and the text.
- SFTP upload: as a download to `[SSH_SFTP_TRANS_INIT] -> [SSH_SFTP_UPLOAD_INIT]`, `[SSH_SFTP_UPLOAD_INIT] -> [SSH_STOP]`,
  rest, `DO phase is complete`, (`} [8 bytes data]`, `upload completely sent off: 8 bytes`), `[SSH_STOP] -> [SSH_SFTP_CLOSE]`,
  `SFTP DONE done`, `[SSH_SFTP_CLOSE] -> [SSH_STOP]`, rest.
- SCP upload: `DO phase starts`, `[SSH_STOP] -> [SSH_SCP_TRANS_INIT]`, `-> [SSH_SCP_UPLOAD_INIT]`, `-> [SSH_STOP]`, rest,
  `DO phase is complete`, (data, sent off), `[SSH_STOP] -> [SSH_SCP_DONE]`, `-> [SSH_SCP_SEND_EOF]`, `-> [SSH_SCP_WAIT_EOF]`,
  `-> [SSH_SCP_WAIT_CLOSE]`, `-> [SSH_SCP_CHANNEL_FREE]`, `SCP DONE phase complete`, `-> [SSH_STOP]`, rest.
- SFTP listing (4 entries): `[SSH_SFTP_TRANS_INIT] -> [SSH_SFTP_READDIR_INIT]`, `-> [SSH_SFTP_READDIR]`, then per entry
  (`{ [N bytes data]`), `[SSH_SFTP_READDIR] -> [SSH_SFTP_READDIR_BOTTOM]`, `-> [SSH_SFTP_READDIR]`; then
  `-> [SSH_SFTP_READDIR_DONE]`, `-> [SSH_STOP]`, rest, `DO phase is complete`, then the SFTP close as above. With `-l` no
  per-entry state changes.
- `-Q`: after `[SSH_STOP] -> [SSH_SFTP_QUOTE_INIT]`, (`SSH: sending quote commands`), `-> [SSH_SFTP_QUOTE]`, the command's
  state (`QUOTE_MKDIR`, `QUOTE_RENAME`, `QUOTE_UNLINK`, `QUOTE_STATVFS`, or `QUOTE_STAT` then `QUOTE_SETSTAT` for chmod/chown;
  none for `pwd`), `-> [SSH_SFTP_NEXT_QUOTE]`, `-> [SSH_SFTP_QUOTE]` for the next, and finally `-> [SSH_SFTP_GETINFO]`.
  After the transfer: `[SSH_STOP] -> [SSH_SFTP_CLOSE]`, `SFTP DONE done`, `-> [SSH_SFTP_POSTQUOTE_INIT]`, sending, the same
  steps, `[SSH_SFTP_NEXT_QUOTE] -> [SSH_SFTP_CLOSE]`, `SFTP DONE done`, `-> [SSH_STOP]`, rest.
- Password: `[SSH_AUTH_PKEY] -> [SSH_AUTH_PASS_INIT]`, `-> [SSH_AUTH_PASS]`, (`SSH: initialized password authentication`),
  `-> [SSH_AUTH_DONE]`. Wrong password: `[SSH_AUTH_PASS] -> [SSH_AUTH_HOST_INIT]`, `-> [SSH_AUTH_AGENT_INIT]`, (trying via
  agent, `failure connecting to agent`), `-> [SSH_AUTH_KEY_INIT]`, `-> [SSH_AUTH_DONE]`, (`Authentication failure`),
  `-> [SSH_SESSION_FREE]`, `[SSH_SESSION_FREE] statemachine() -> 67, block=0`.
- Keyboard-interactive (server offers only it): `[SSH_AUTHLIST] -> [SSH_AUTH_PKEY_INIT]`, `-> [SSH_AUTH_PASS_INIT]`,
  `-> [SSH_AUTH_HOST_INIT]`, `-> [SSH_AUTH_AGENT_INIT]`, `-> [SSH_AUTH_KEY_INIT]`, `-> [SSH_AUTH_KEY]`, (`SSH: initialized
  keyboard interactive authentication`), `-> [SSH_AUTH_DONE]`. Denied: `[SSH_AUTH_KEY] statemachine() -> 67, block=0` with
  no failure line and no state change.
- Fingerprints: a match writes (`SSH: SHA256 checksum match` / `MD5 checksum match`) between `[SSH_S_STARTUP] -> [SSH_HOSTKEY]`
  and `[SSH_HOSTKEY] -> [SSH_AUTHLIST]`, without the knownhosts line; a mismatch writes (`Denied establishing ...`),
  `[SSH_HOSTKEY] -> [SSH_SESSION_FREE]`, `[SSH_SESSION_FREE] statemachine() -> 60, block=0`.
- Failures: the failure line, then `[failed state] -> [SSH_SFTP_CLOSE]` (SFTP), `-> [SSH_SCP_CHANNEL_FREE]` (SCP) and
  `[that] statemachine() -> <code>, block=0`: download 78 from `SSH_SFTP_DOWNLOAD_INIT`, listing 78 from `SSH_SFTP_READDIR_INIT`,
  SCP download 78 from `SSH_SCP_DOWNLOAD_INIT`, SCP upload 25 from `SSH_SCP_UPLOAD_INIT`, `-Q` 21 from the command's state
  (`SSH_SFTP_QUOTE` for an unknown command, `QUOTE_STAT` for a bad chmod), before or after the transfer. The SFTP upload
  (exit 9) enters `SSH_SFTP_CLOSE` before its `Upload failed` line.
- Not measurable here, written from curl's source (ADR-0373), filed as BL-1205: agent success (Windows `ssh-agent` is
  disabled), a symbolic link in a listing, `-I` on a directory.
- Verified: Curl's built `curl.dll` and real curl wrote identical stderr (ports and `Trying` aside) for the 22 command lines
  above with `-o`; with stdout to the null device Curl alone writes `{ [N bytes data]` lines, which predates this task.
- Decided (ADR-0373): `SshStateTrace.Fail` writes the failure's way out from the trace's state, so each failure path needs no
  trace code of its own. Pinned in `SshProtocolHandlerTests.StateTraceOtherPaths.cs` (26 cases).
- `--ai-help`: no option changed; nothing to update.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. --trace-config ssh writes curl's [SSH] lines for uploads, listings, -Q, password and keyboard-interactive logins, fingerprints and failures
