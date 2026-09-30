# ADR-0271 — SSH users authenticate with an ssh-agent's identities after `password`, as libssh2 1.11.1 does

- **Status:** Accepted
- **Date:** 2026-09-30

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-902.
Replaces ADR-0262's decision 4 ("the agent always fails"); the rest of ADR-0262 stands.

## Context

When the server's method list names `publickey`, curl 8.21.0 tries an ssh-agent after
`password` and before `keyboard-interactive` (`lib/vssh/libssh2.c`: `SSH_AUTH_AGENT_INIT`,
`SSH_AUTH_AGENT_LIST`, `SSH_AUTH_AGENT`): `libssh2_agent_connect`, then
`libssh2_agent_list_identities`, then `libssh2_agent_userauth` for each identity until one
authenticates the user.

Measured 2026-09-30 with the Windows reference build (curl 8.21.0, libssh2 1.11.1, WinCNG)
through `Record-CurlExchange.ps1 -NoServer`, `curl -sS -v -k -u tester:wrong
sftp://127.0.0.1:22902/x`, against a throwaway loopback server built from this library's
`InMemorySshServer` (listing `publickey,password,keyboard-interactive`, refusing the
password), with OpenSSH's own `ssh-agent` (in WSL, holding RSA, Ed25519 and ECDSA keys
made with `ssh-keygen`) reached through a throwaway named-pipe relay named by
`SSH_AUTH_SOCK=\\.\pipe\...` that logged every byte. The Windows `ssh-agent` service could
not be started without administrator rights. Every run showed `SSH: trying private key file
''`, `SSH: publickey authentication denied: Reason unknown (-1)`, the silent password
refusal, then `SSH: trying publickey authentication via agent`, then:

| Agent | curl's messages | Last `-v` lines | Exit |
| --- | --- | --- | --- |
| one RSA key, authorized | agent: `00000001 0B`; server: `publickey` query `ssh-rsa`; agent: sign request, flags 0; server: signed `ssh-rsa` | `SSH: agent authenticated user 'tester' with key 'k1-comment'`, `SSH: authentication complete` | 0 |
| RSA then Ed25519, Ed25519 authorized | query `ssh-rsa` (refused), query `ssh-ed25519`, one sign request, signed `ssh-ed25519` | `... with key 'k2-comment'` | 0 |
| RSA, Ed25519, ECDSA, ECDSA authorized | a query for each in the agent's order, one sign request | `... with key 'k3-comment'` | 0 |
| RSA and Ed25519, neither authorized | a query for each, no sign request, then `keyboard-interactive` | `SSH: no agent identity would match`, `curl: (67) Login denied` | 67 |
| no identity | the list only, then `keyboard-interactive` | `SSH: no agent identity would match` | 67 |
| RSA, `server-sig-algs` `rsa-sha2-512,...` | query and signature `rsa-sha2-512`, sign flags 4 | authenticated | 0 |
| RSA, `server-sig-algs` `rsa-sha2-256` | `rsa-sha2-256`, flags 2 | authenticated | 0 |
| RSA then Ed25519 (authorized), `server-sig-algs` `ssh-ed25519` | **no** `publickey` request at all, the list only | `SSH: no agent identity would match` | 67 |
| RSA then Ed25519, the signed RSA request refused | signed `ssh-rsa` refused, then query `ssh-ed25519` | `SSH: no agent identity would match` | 67 |
| the pipe closes before answering the list | the list request only | `SSH: failure requesting identities to agent` | 67 |
| no agent (`SSH_AUTH_SOCK` unset, or naming no pipe) | nothing | `SSH: failure connecting to agent` | 67 |

The row with `server-sig-algs` `ssh-ed25519` is libssh2's: `_libssh2_userauth_publickey`
returns `No signing signature matched` for the RSA key without freeing
`session->userauth_pblc_method`, and every later call reuses that `ssh-rsa` method instead
of reading the next key's type, so it fails the same way.

## Decision

- **Order and lines.** `SshUserAuthentication` asks the agent after `password` fails and
  before `keyboard-interactive`, only when the list names `publickey`, writing the measured
  lines: `failure connecting to agent`, `failure requesting identities to agent`,
  `no agent identity would match`, or `agent authenticated user '<user>' with key
  '<comment>'`. The comment ends at its first zero byte (libssh2 keeps a C string) and is
  read as UTF-8. The agent connection is closed when the step ends.
- **Each identity** is tried as libssh2's `_libssh2_userauth_publickey` tries it: a blob
  shorter than 4 bytes, or whose key type overruns it, fails without a request; the method
  is the blob's key type upgraded from `server-sig-algs` as a key file's is (ADR-0230); the
  question (no signature), then on `PK_OK` an `SSH2_AGENTC_SIGN_REQUEST` over the session
  identifier and the request, with flag 4 for `rsa-sha2-512`, 2 for `rsa-sha2-256`, 0
  otherwise; a `SUCCESS` to the question authenticates without a signature. The signature
  sent names libssh2's plain method (a certificate's key type without
  `-cert-v01@openssh.com`), the signature as a string, or raw for the `sk-` methods.
- **A signature by another method** than the one asked for (or its plain form) is retried
  once from the key's own type without an upgrade, question and all; a second mismatch, a
  sign failure, an unreadable answer or a missing signature fails the identity, and curl
  goes on to the next.
- **The leftover method** is modelled: once an RSA key, from a file or the agent, finds no
  signature algorithm, its method stays, and each later agent identity starts from it.
- **Where the agent is** (`SystemSshAgentConnector`, behind `ISshAgentConnector`): on
  Windows, the pipe `SSH_AUTH_SOCK` names, or `\\.\pipe\openssh-ssh-agent` when it is unset
  (libssh2's `agent_win.c`), opened without waiting; elsewhere, the Unix socket
  `SSH_AUTH_SOCK` names, and no agent when it is unset or empty. libssh2 hands any Windows
  path to `CreateFileA`; only a `\\server\pipe\name` path is opened here, since opening a
  regular file as an agent would write the request into it. Any failure to connect is
  `failure connecting to agent`.
- **Not built here: Pageant.** libssh2 on Windows tries PuTTY's Pageant (a window found with
  `FindWindowA` and `WM_COPYDATA`) before the OpenSSH pipe. That is Win32 messaging, not
  BCL I/O, so it is its own task, BL-1035.
- The connector's two I/O methods are excluded from coverage under ADR-0083 and measured by
  Integration tests against a pipe and a Unix socket the test serves.

## Consequences

- `SshUserAuthenticationTests.Agent` pins every row above against `Fakes.InMemorySshAgent`
  (real keys, real signatures) or `Fakes.ScriptedSshAgent` (fixed bytes), and
  `SshAgentClientTests` the agent protocol bytes; `SshProtocolHandlerTests` runs a whole
  transfer authenticated by the agent.
- Handler tests use `Fakes.UnreachableSshAgent`, so a developer's own agent cannot change
  their outcome.
- An RSA certificate identity signs as its own type: libssh2's upgrade of
  `ssh-rsa-cert-v01@openssh.com` to `rsa-sha2-*-cert-v01@openssh.com` is BL-1036.
