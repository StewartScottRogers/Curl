# ADR-0215 — SSH users authenticate with none, password, then keyboard-interactive, and a refusal is exit 67

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-567.

## Context

After the key exchange (ADR-0206, ADR-0212) and the host-key check (ADR-0213), curl
authenticates the user through libssh2. BL-567 builds that for `password` and
`keyboard-interactive` (RFC 4252, RFC 4256); `publickey` is BL-568's. Which messages curl
sends, in which order, and what each refusal prints had to be measured.

No local OpenSSH server exists on the lane machine (Git for Windows ships none; WSL's
Ubuntu has no `sshd` and no password-less `sudo`). As in BL-566, the Windows reference
build (curl 8.21.0, libssh2 1.11.1 on WinCNG) was run through `Record-CurlExchange.ps1
-NoServer` with `-sS -k -u <user:password> sftp://127.0.0.1:<port>/x`, `HOME` set to an
empty directory, against a throwaway loopback server built from this library's classes
(`diffie-hellman-group14-sha256`, `rsa-sha2-256`, `aes128-ctr`, `hmac-sha2-256`), which
logged every message curl sent and answered each case as scripted. After a success curl
opened a session channel, which the server closed, so those runs ended `curl: (2) Failure
initializing sftp session: Unable to startup channel`.

| Case | Client messages after `SERVICE_ACCEPT` | Exit | stderr |
| --- | --- | ---: | --- |
| list `publickey,password,keyboard-interactive`, right password | `none`, `password` | success | |
| same list, wrong password | `none`, `password`, `keyboard-interactive`, one `INFO_RESPONSE` with the password | 67 | `curl: (67) Login denied` |
| list `publickey,keyboard-interactive` (`PasswordAuthentication no`), right / wrong password | `none`, `keyboard-interactive`, `INFO_RESPONSE` | success / 67 | / `Login denied` |
| list `password` alone, wrong password | `none`, `password` | 67 | `curl: (67) Authentication failure` |
| no `-u` | user and password both empty strings | 67 | `Login denied` |
| user in the URL | the URL's user and password | success | |
| `USERAUTH_BANNER` before the list or before `SUCCESS` | unchanged; nothing printed | success | |
| `IGNORE` and an unknown message number before `SUCCESS` | unchanged | success | |
| password answered with partial success, `keyboard-interactive` in the first list | `keyboard-interactive` tried next | success | |
| password answered with partial success, list was `password` only | nothing more | 67 | `Authentication failure` |
| `none` answered with `SUCCESS` | nothing more | success | |
| list `publickey`, `gssapi-with-mic,hostbased`, or empty | nothing more | 67 | `Authentication failure` |
| peer closes, disconnects, or sends a `USERAUTH_FAILURE` too short for its list, after `none` | | 79 | `curl: (79) Error in the SSH layer` |
| `PASSWD_CHANGEREQ` after `password` | `keyboard-interactive` tried next | success | |
| peer closes or disconnects after `password`, `keyboard-interactive` listed | | 67 | `Login denied` |
| peer closes after `password`, list `password` | | 67 | `Authentication failure` |
| peer closes after `keyboard-interactive`; `INFO_REQUEST` cut short | no `INFO_RESPONSE` | 67 | `Login denied` |
| `INFO_REQUEST` with 0, 2 or 100 prompts | `INFO_RESPONSE` with that many empty strings | success | |
| `INFO_REQUEST` with 101 prompts | no `INFO_RESPONSE` | 67 | `Login denied` |
| two `INFO_REQUEST` rounds of one prompt | the password in each | success | |
| `-u 'téster:sécret'` | `E9` for `é`: the ANSI code page | 67 | `Login denied` |
| `SERVICE_ACCEPT` replaced by: close / `DISCONNECT` / `ssh-connection` / 3 bytes / a `USERAUTH_FAILURE` then close | | 2 | `Failure establishing ssh session: ` `-43, Failed to get response to ssh-userauth request` / `-13, ...` the same / `-14, Invalid response received from server` / `-14, Unexpected packet length` / `-43, ...` |

With `-v` curl logged `SSH: host offers authentication via: <list>`, `SSH: trying private
key file ''`, `SSH: publickey authentication denied: Reason unknown (-1)`, `SSH:
initialized password authentication`, `SSH: user accepted with no authentication` and `SSH:
authentication complete` (BL-578's). curl 8.21.0 has no `--ssh-auth-types` option; every
method is allowed.

## Decision

`Authentication.SshUserAuthentication` does both halves.

- **`RequestServiceAsync`** sends `SERVICE_REQUEST ssh-userauth`, the last step of libssh2's
  session startup, so the handler calls it after `ExchangeKeysAsync` and before
  `SshHostKeyChecker.Check`. Its failures are exit 2 `Failure establishing ssh session:
  <code>, <description>` as measured; a MAC or tag failure keeps its `-4` or `-12`
  (ADR-0212), and broken framing reads as a close (`-43`, not measured).
- **`AuthenticateAsync`** sends `none` and reads the list, then tries `password` when the
  list contains that text and `keyboard-interactive` when it contains that text, matched
  by substring as curl's `strstr` does. The first list is the only one read: a partial
  success is a failure. `keyboard-interactive` answers the password to a round of exactly
  one prompt and an empty string to every prompt of any other round, up to 100 prompts.
  Outcomes are exit 79 `Error in the SSH layer` when the answer to `none` cannot be read,
  exit 67 `Login denied` when `keyboard-interactive` was tried and failed, and exit 67
  `Authentication failure` when it was not tried. A close, a disconnect, broken framing, a
  failed MAC or a password change request fails only the method in hand.
- While waiting for an answer every other message is skipped (libssh2 queues them), and a
  `KEXINIT` is answered with `SshTransport.ReExchangeKeysAsync` first.
- The user and password are encoded with an injected `Encoding`: the handler passes
  ADR-0022's `CredentialEncoding.ForPlatform`, the system ANSI code page on Windows and
  UTF-8 elsewhere. No `-u` is an empty user and an empty password.

## Consequences

- `publickey` (BL-568) slots in before `password`, and ssh-agent identities (BL-902) after
  `password` and before `keyboard-interactive`, both only when the list names `publickey`,
  as curl orders them.
- On this machine curl found no key and no agent, so it sent no `publickey` request; a user
  with `~/.ssh/id_rsa` or a running agent would see one. The measurement set `HOME` to an
  empty directory for that reason.
- At the end of every run curl sends `DISCONNECT` with reason 11 and description
  `Shutdown`; that is the handler's teardown, not this class's.
