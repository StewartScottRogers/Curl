# ADR-0304 — The ssh-agent step on Windows asks PuTTY's Pageant before the OpenSSH pipe, as libssh2 1.11.1 does

- **Status:** Accepted
- **Date:** 2026-10-01

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1035.
Replaces ADR-0271's "Not built here: Pageant"; the rest of ADR-0271 stands.

## Context

libssh2 1.11.1's `libssh2_agent_connect` walks `supported_backends` and stops at the first
that connects; on Windows that is Pageant (`agent_connect_pageant`:
`FindWindowA("Pageant", "Pageant")`), then the OpenSSH pipe. `agent_transact_pageant`
refuses a request frame over `PAGEANT_MAX_MSGLEN` (8192), finds the window again, creates
an 8192-byte file mapping named `PageantRequest%08x` (the thread id), writes the request
frame into it, sends `WM_COPYDATA` with `dwData` `0x804e50ba` and the mapping's name as a
zero-terminated ANSI string, and on a nonzero result reads the answer's length and body
back from the mapping (a length over 8192 is refused). A zero result leaves no answer.

Measured 2026-10-01 with the Windows reference build (curl 8.21.0, libssh2 1.11.1, WinCNG)
through `Record-CurlExchange.ps1 -NoServer`, `curl -sS -v -k -u tester:wrong
sftp://127.0.0.1:<port>/f`, against a throwaway loopback bridge to this library's
`InMemorySshServer` (password refused, one RSA key authorized). PuTTY is not installed, so
Pageant was the test project's `Fakes.FakePageantWindow`: a real top-level window of class
and title `Pageant` answering `WM_COPYDATA` from `Fakes.InMemorySshAgent`. `SSH_AUTH_SOCK`
named a pipe the measurement served.

| Case | Mapping names seen | Last `-v` lines | Exit |
| --- | --- | --- | --- |
| Pageant holding the authorized key, pipe also served | `PageantRequest0000bb88` twice (list, then sign) | `SSH: agent authenticated user 'tester' with key 'pageant-key'`, `SSH: authentication complete` | 0 |
| Pageant only | the same, list then sign | the same | 0 |
| Pageant returning zero from `WM_COPYDATA` | one (the list) | `SSH: failure requesting identities to agent`, `curl: (67) Authentication failure` | 67 |
| Pageant holding no identity | one (the list) | `SSH: no agent identity would match` | 67 |

The pipe was never opened while Pageant's window existed. (In this harness curl did not
open the served pipe even without Pageant - `failure connecting to agent` - so the control
case is not evidence either way; the order rests on libssh2's source and the Pageant rows.)
The requests in the mapping were byte for byte the ones ADR-0271 recorded on the pipe.

## Decision

- **Order.** `PlatformSshAgentConnector.Create` builds, on Windows,
  `FirstReachableSshAgentConnector` over `PageantSshAgentConnector` then
  `SystemSshAgentConnector`; elsewhere `SystemSshAgentConnector` alone. The first agent that
  connects is used and no later one is tried.
- **Pageant** (`PageantSshAgentConnector`, behind `IPageantWindow`): no agent when the window
  is not found; otherwise a `PageantAgentStream` that exchanges each whole request frame as
  `agent_transact_pageant` does. A failed exchange (frame over 8192, window gone, mapping not
  made, zero result) leaves nothing to read, so `SshAgentClient` fails the request: the
  measured `failure requesting identities to agent`.
- **One deliberate difference:** libssh2 accepts an answer length up to 8192 and copies
  past the mapping's end for lengths over 8188; here an answer that does not fit the mapping
  (over 8188) fails the request. No real Pageant sends one.
- **The Win32 adapter** `WindowsPageantWindow` uses `LibraryImport` of `FindWindowW`,
  `SendMessageW` and `GetCurrentThreadId` (AOT-safe; `AllowUnsafeBlocks` for the generated
  marshalling) and the BCL's `MemoryMappedFile`. It is excluded from coverage under
  ADR-0083 and measured by Windows-only Integration tests against `FakePageantWindow`.

## Consequences

- `PlatformSshAgentConnectorTests` pins Pageant-first with fakes (the pipe agent is never
  connected while Pageant answers); `PageantSshAgentConnectorTests` and
  `PageantAgentStreamTests` pin the transaction; `WindowsPageantWindowTests` (Integration,
  Windows) run the Win32 calls.
- Integration tests that create a `FakePageantWindow` run alone (`DoNotParallelize`), since
  `FindWindow` finds any window of that class and title, a real Pageant included.
