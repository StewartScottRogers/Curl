---
id: BL-902
title: Authenticate an SSH user with the identities an ssh-agent holds
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-567, BL-568]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-29
completed: 2026-09-30
---
# BL-902 — Authenticate an SSH user with the identities an ssh-agent holds

## Goal

When the server's method list (the answer to `none`) names `publickey`, curl 8.21.0 tries the identities of a running ssh-agent after `password` and before `keyboard-interactive` (libcurl's SSH_AUTH_AGENT_INIT / SSH_AUTH_AGENT states: libssh2_agent_init, connect, list identities, try each with libssh2_agent_userauth). Build that step in `Curl.Protocol.Ssh.UnitLibrary`'s `Authentication.SshUserAuthentication`: the agent protocol (draft-miller-ssh-agent: SSH_AGENTC_REQUEST_IDENTITIES, SSH2_AGENTC_SIGN_REQUEST) behind an injected seam so tests use a fake agent; on Windows the agent is the OpenSSH named pipe `\\.\pipe\openssh-ssh-agent` (and Pageant where libssh2 1.11.1 uses it), elsewhere the Unix socket in `SSH_AUTH_SOCK`. An agent that cannot be reached is skipped silently and curl goes on to keyboard-interactive.

## Context

BL-567 (ADR-0215) built none/password/keyboard-interactive and measured that with no agent reachable curl sends no publickey request; BL-568 builds publickey from key files. No existing task covers the agent. BCL only: named pipes (`System.IO.Pipes`) and Unix domain sockets are in the BCL; never a package. Measure with the reference curl (`Record-CurlExchange.ps1 -NoServer`) against a loopback server built from the library's classes, as ADR-0215 did, with a real ssh-agent holding a test key (Windows OpenSSH ssh-agent service or `ssh-agent` in WSL), recording the userauth messages curl sends.

## Acceptance criteria

- [x] Measured first: the publickey requests curl sends from an agent with one and with two identities, a server that refuses them, and no agent running; stderr and exit code of each in Notes.
- [x] `Curl.Protocol.Ssh.UnitTests` pin the agent requests and the userauth messages against a fake agent and the in-memory peer, including the method order password -> agent -> keyboard-interactive.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- **Measured 2026-09-30, Windows reference build** (curl 8.21.0, libssh2 1.11.1 WinCNG):
  `Record-CurlExchange.ps1 -NoServer -CurlArgs -sS,-v,-k,-u,tester:wrong,sftp://127.0.0.1:22902/x`,
  against a throwaway loopback server built from `InMemorySshServer` (list
  `publickey,password,keyboard-interactive`, password refused), with OpenSSH's real
  `ssh-agent` in WSL (RSA 2048, Ed25519 and ECDSA P-256 keys from `ssh-keygen`) reached
  through a throwaway byte-logging named-pipe relay named by `SSH_AUTH_SOCK`; all deleted
  before commit. The Windows `ssh-agent` service is disabled and cannot be started without
  administrator rights. Every run: `* SSH: trying private key file ''`,
  `* SSH: publickey authentication denied: Reason unknown (-1)`, the silent password
  refusal, `* SSH: trying publickey authentication via agent`, then:
  - one RSA identity, authorized: agent `00000001 0B`; server: query `ssh-rsa`; agent:
    `0D` sign request, flags 0; server: signed `ssh-rsa` -> `* SSH: agent authenticated user
    'tester' with key 'k1-comment'`, `* SSH: authentication complete`, exit 0.
  - two identities (RSA, Ed25519), the second authorized: query `ssh-rsa` refused, query
    `ssh-ed25519`, one sign request -> `... with key 'k2-comment'`, exit 0. Three (RSA,
    Ed25519, ECDSA), the third authorized: three queries, one sign -> exit 0.
  - both refused: two queries, no sign, `keyboard-interactive` refused -> `* SSH: no agent
    identity would match`, `* closing connection #0`, `curl: (67) Login denied`, exit 67.
  - no identity: the list only -> `no agent identity would match`, exit 67 `Login denied`.
  - no agent (`SSH_AUTH_SOCK` unset, default pipe absent; or set to an empty string) ->
    `* SSH: failure connecting to agent`, exit 67 `Login denied`.
  - agent pipe closes before answering the list -> `* SSH: failure requesting identities to
    agent`, exit 67 `Login denied`.
  - `server-sig-algs` `rsa-sha2-512,rsa-sha2-256,ssh-rsa,...`: query and signature
    `rsa-sha2-512`, sign flags 4; `rsa-sha2-256` alone: flags 2; both exit 0.
  - `server-sig-algs` `ssh-ed25519` with RSA then Ed25519 (authorized): no `publickey`
    request at all -> `no agent identity would match`, exit 67. Cause found in libssh2's
    `userauth.c`: the failed RSA upgrade leaves `userauth_pblc_method` set, and later calls
    reuse it (modelled as the "leftover method", ADR-0270).
  - signed RSA request refused: then query `ssh-ed25519` -> `no agent identity would
    match`, exit 67.
- **Design (ADR-0270).** `Authentication/ISshAgentConnector` (seam), `SystemSshAgentConnector`
  (Windows: the pipe `SSH_AUTH_SOCK` names or `\.\pipe\openssh-ssh-agent`; elsewhere the
  Unix socket in `SSH_AUTH_SOCK`), `SshAgentClient` (framing, `REQUEST_IDENTITIES`,
  `SIGN_REQUEST`, parsed as libssh2's `agent.c`), `SshAgentIdentity`, `SshAgentSignature`,
  `SshAgentMessageNumber`; `SshUserAuthentication` takes the connector and runs the agent
  step between `password` and `keyboard-interactive`; `SshProtocolHandler` passes the
  system connector from its public constructor. Test fakes: `InMemorySshAgent` (real keys,
  real signatures), `ScriptedSshAgent` (fixed bytes), `UnreachableSshAgent`.
- **Decisions (ADR-0270):** only `\server\pipe\name` paths are opened on Windows (libssh2
  would `CreateFileA` any path, a regular file included); the comment is shown up to its
  first zero byte, read as UTF-8; the agent connection closes at the end of the agent step
  rather than at session end (not observable to the server); the pipe is opened without
  libssh2's 1-second wait for a busy pipe.
- **Handler tests** now use the internal constructor with `UnreachableSshAgent`, so a
  developer's own agent cannot change them; `ExecuteAsync_ConnectFails_...` still covers the
  public constructor.
- **Touches:** added `Documentation/Planning/Decisions` for ADR-0270, its index row and the
  note on ADR-0262 decision 4; no task in `Doing` names it.
- **Follow-ups filed:** BL-1035 (Pageant, which libssh2 tries before the OpenSSH pipe on
  Windows), BL-1036 (libssh2's upgrade of an RSA certificate's signature method).
- **Quality:** `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary`: 100% line,
  100% branch, 787 members, 0 failing, worst CRAP 10.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. SSH users authenticate with an ssh-agent's identities after password, as curl 8.21.0 measured (ADR-0270)
