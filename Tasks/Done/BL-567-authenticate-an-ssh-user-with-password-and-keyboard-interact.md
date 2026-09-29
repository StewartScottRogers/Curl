---
id: BL-567
title: Authenticate an SSH user with password and keyboard-interactive
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-565, BL-566]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests, Documentation/Planning/Decisions/ADR-0214-ssh-users-authenticate-with-none-password-then-keyboard-interactive-and-a-refusal-is-exit-67.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-567 — Authenticate an SSH user with password and keyboard-interactive

## Goal

After the transport is up, the handler requests `ssh-userauth` and authenticates the `-u` user with `password` and `keyboard-interactive` (RFC 4252, RFC 4256) in the order curl 8.21.0 tries them, handles `SSH_MSG_USERAUTH_BANNER`, and maps a refusal to exit 67 (or the code curl gives) with curl's message.

## Context

- Conformance audit 2026-09-28, row 35. Needs BL-565 (encrypted transport) and BL-566 (trusted host).
- **BCL only.** No new primitive needed. Anything the BCL lacks is hand-built in `Curl.Cryptography.UnitLibrary` (standing rule, root `CLAUDE.md`, "Decisions", 2026-09-28): never a package, never a task blocked for a missing primitive.
- Measure with the reference curl against a local OpenSSH server through `Record-CurlExchange.ps1 -NoServer`: right password, wrong password, `PasswordAuthentication no` with `KbdInteractiveAuthentication yes`, no `-u` at all, and a server with a banner; stderr and exit code.

## Acceptance criteria

- [x] Measured first as above; stderr and exit code of each copied into Notes.
- [x] `Curl.Protocol.Ssh.UnitTests` pin the client messages and outcome for each case against the in-memory peer, including partial success and the method order.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- **Design (ADR-0214).** New `Authentication` folder: `SshUserAuthentication`
  (`RequestServiceAsync`, the `ssh-userauth` service request libssh2 sends at the end of
  session startup, before the host-key check; `AuthenticateAsync`, `none` then `password`
  then `keyboard-interactive`) and `SshAuthenticationMessageNumber`. `SshMessageNumber`
  gains `ServiceRequest`/`ServiceAccept`, `Libssh2ErrorCode` gains `-14`, `-43` and their
  descriptions, `SshTransferException` gains `AuthenticationFailure`, `LoginDenied` and
  `SshLayerError`. Credentials are encoded with an injected `Encoding` (ADR-0022's
  `CredentialEncoding.ForPlatform`, passed by the handler). No handler calls it yet
  (BL-569/BL-574/BL-576 compose the session).
- **Measured 2026-09-29, Windows reference build** (curl 8.21.0, libssh2 1.11.1 WinCNG),
  `curl -sS -k -u <user:pw> sftp://127.0.0.1:<port>/x` through `Record-CurlExchange.ps1
  -NoServer`, `HOME` an empty directory, against a throwaway MSTest loopback SSH server
  built from this library's classes (group14-sha256, rsa-sha2-256, aes128-ctr,
  hmac-sha2-256) that logged curl's messages; deleted before commit. No local sshd: Git
  for Windows ships none, WSL Ubuntu has no sshd and no password-less sudo. Successful
  logins went on to open a session channel, which the server closed: `curl: (2) Failure
  initializing sftp session: Unable to startup channel`.
  - Right password (list `publickey,password,keyboard-interactive`): `none`, `password`
    -> success. No `publickey` request (no key file, no agent).
  - Wrong password, same list or `password,keyboard-interactive`: `none`, `password`,
    `keyboard-interactive` (lang `""`, submethods `""`), `INFO_RESPONSE ["wrong"]` ->
    exit 67 `curl: (67) Login denied`.
  - `PasswordAuthentication no` (list `publickey,keyboard-interactive`, or
    `keyboard-interactive` alone): `none`, `keyboard-interactive`, `INFO_RESPONSE
    ["secret"]` -> success; wrong password -> 67 `Login denied`.
  - List `password` only, wrong password -> 67 `curl: (67) Authentication failure`.
  - No `-u`: user `""`, password `""`, then as a wrong password -> 67 `Login denied`.
    `-u tester:` sends an empty password. User in the URL: used as given.
  - Banner before the list or before SUCCESS: ignored, nothing printed -> success.
    IGNORE and message 99 during password: skipped -> success.
  - Partial success on password with `keyboard-interactive` in the first list: tried
    next -> success. With list `password` only: 67 `Authentication failure`.
  - `none` -> SUCCESS: nothing more sent. List `publickey`, `gssapi-with-mic,hostbased`
    or empty: 67 `Authentication failure`, no attempt.
  - After `none`: close, DISCONNECT, or `USERAUTH_FAILURE` `33 00 00` -> 79 `curl: (79)
    Error in the SSH layer`.
  - PASSWD_CHANGEREQ after password: kbd tried next -> success. Close or DISCONNECT
    after password with kbd listed -> 67 `Login denied`; with only password -> 67
    `Authentication failure`. Close after the kbd request, or an INFO_REQUEST cut short
    -> 67 `Login denied`.
  - INFO_REQUEST with 0 / 2 / 100 prompts: that many empty answers -> success; 101
    prompts: no answer, 67 `Login denied`. Two one-prompt rounds: password each time.
  - `-u 'téster:sécret'`: `é` sent as the single byte `E9` (ANSI code page, ADR-0022).
  - Service request answered by close: exit 2 `Failure establishing ssh session: -43,
    Failed to get response to ssh-userauth request`; DISCONNECT: `-13, ...` same text;
    `SERVICE_ACCEPT ssh-connection`: `-14, Invalid response received from server`; a
    3-byte SERVICE_ACCEPT: `-14, Unexpected packet length`; a USERAUTH_FAILURE instead:
    skipped, then the close gives `-43`.
  - `--ssh-auth-types` does not exist in curl 8.21.0 (`option --ssh-auth-types: is
    unknown`, exit 2), so every method is always allowed.
  - `-v` lines (BL-578's): `SSH: host offers authentication via: <list>`, `SSH: trying
    private key file ''`, `SSH: publickey authentication denied: Reason unknown (-1)`,
    `SSH: initialized password authentication`, `SSH: user accepted with no
    authentication`, `SSH: authentication complete`, `Authentication failure`.
  - At teardown curl always sends DISCONNECT reason 11 `Shutdown` (the handler's).
- **Not measured, decided:** broken framing while waiting for SERVICE_ACCEPT reads as a
  close (`-43`); a MAC failure there keeps ADR-0212's `-4`/`-12`; a MAC failure or broken
  framing during a method fails that method; a KEXINIT during authentication runs
  `SshTransport.ReExchangeKeysAsync` and carries on.
- **Follow-up filed:** BL-900, ssh-agent identities (curl tries them between `password`
  and `keyboard-interactive` when `publickey` is listed); no task covered it.
- **Touches.** Added ADR-0214 and `Documentation/Planning/Decisions/README.md`: the
  decision needed an ADR, and no task in Doing names either.
- **Quality.** `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary`: 100%
  line, 100% branch, 233 members, 0 failing, worst CRAP 10.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. SSH users authenticate as curl 8.21.0 does: ssh-userauth service request, none, password, keyboard-interactive (banner skipped, partial success, re-exchange); refusals exit 67 Login denied / Authentication failure, unreadable list exit 79; SSH library 100/100
