---
id: BL-1035
title: Ask PuTTY's Pageant for SSH agent identities on Windows before the OpenSSH pipe
priority: Low
assignee: Claude
pipeline: protocol
depends-on: [BL-902]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-09-30
completed: 2026-10-01
---
# BL-1035 — Ask PuTTY's Pageant for SSH agent identities on Windows before the OpenSSH pipe

## Goal

On Windows, the agent step of SSH authentication asks PuTTY's Pageant first, as libssh2 1.11.1's `agent.c` does (`supported_backends`: Pageant, then OpenSSH), and uses the OpenSSH pipe only when no Pageant window is found.

## Context

BL-902 (ADR-0270) built the agent step behind `ISshAgentConnector` with `SystemSshAgentConnector` opening the OpenSSH named pipe (or the Unix socket in `SSH_AUTH_SOCK` elsewhere) and left Pageant out. libssh2's `agent_connect_pageant` finds the window with `FindWindowA("Pageant", "Pageant")`; `agent_transact_pageant` writes the request into a named file mapping `PageantRequest%08x` (thread id), sends `WM_COPYDATA` with `AGENT_COPYDATA_ID` 0x804e50ba and reads the answer back from the mapping (max 8192 bytes). That is Win32 messaging: `LibraryImport` of `user32`/`kernel32` (AOT-safe) and `MemoryMappedFile`, no package. Build it as another `ISshAgentConnector` tried before the pipe on Windows only. Measure with the reference curl and a running Pageant holding a test key (`Record-CurlExchange.ps1 -NoServer`, as ADR-0270 did).

## Acceptance criteria

- [x] Measured first: curl's agent lines and messages with Pageant running and the OpenSSH pipe also present, and with Pageant only; in Notes.
- [x] `Curl.Protocol.Ssh.UnitTests` pin the Pageant-first order with fakes; the Win32 adapter is covered by a Windows-only Integration test or excluded under ADR-0083 with a justification.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- **Measured 2026-10-01** (curl 8.21.0, libssh2 1.11.1, WinCNG; `Record-CurlExchange.ps1 -NoServer`, `-sS -v -k -u tester:wrong sftp://127.0.0.1:<port>/f` against a throwaway TCP bridge to `InMemorySshServer`). PuTTY is not installed, so Pageant was `Fakes.FakePageantWindow`, a real window of class and title `Pageant` answering from `InMemorySshAgent`; kept for the Integration tests. Results (full table in ADR-0304):
  - Pageant + pipe in `SSH_AUTH_SOCK`: mapping `PageantRequest0000bb88` used twice (list `0B`, then sign `0D...`, flags 0); `SSH: agent authenticated user 'tester' with key 'pageant-key'`, exit 0; the pipe was never opened.
  - Pageant only: the same lines, exit 0.
  - Pageant returning zero: `SSH: failure requesting identities to agent`, exit 67.
  - Pageant with no identity: `SSH: no agent identity would match`, exit 67.
  - Caveat: in this harness curl did not open the served pipe even without Pageant (`failure connecting to agent`), so "pipe never opened" proves less than it seems; the order also rests on libssh2's `supported_backends` loop.
- **Design (ADR-0304):** `PlatformSshAgentConnector.Create` → on Windows `FirstReachableSshAgentConnector[PageantSshAgentConnector, SystemSshAgentConnector]`; `PageantSshAgentConnector` + `PageantAgentStream` hold the transaction logic behind `IPageantWindow`; `WindowsPageantWindow` is the thin Win32 adapter (`LibraryImport`, so the csproj gains `AllowUnsafeBlocks`), excluded under ADR-0083 and run by `WindowsPageantWindowTests` (Integration, Windows only, `DoNotParallelize`).
- **One difference from libssh2:** an answer length over 8188 fails instead of copying past the mapping's end.
- **Gates:** solution build `-warnaserror` clean; fast tests all green (Ssh 1514); Pageant tests incl. Integration 19/19; `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary`: 0 failing members.
- **Found:** an existing Ssh Integration test hangs on Windows (not one of these); filed BL-1086.

## Log

- 2026-09-30: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. On Windows the ssh-agent step asks Pageant first, then the OpenSSH pipe (ADR-0304)
