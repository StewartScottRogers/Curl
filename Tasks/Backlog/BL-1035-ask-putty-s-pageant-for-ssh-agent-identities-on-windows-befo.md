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
completed:
---
# BL-1035 — Ask PuTTY's Pageant for SSH agent identities on Windows before the OpenSSH pipe

## Goal

On Windows, the agent step of SSH authentication asks PuTTY's Pageant first, as libssh2 1.11.1's `agent.c` does (`supported_backends`: Pageant, then OpenSSH), and uses the OpenSSH pipe only when no Pageant window is found.

## Context

BL-902 (ADR-0270) built the agent step behind `ISshAgentConnector` with `SystemSshAgentConnector` opening the OpenSSH named pipe (or the Unix socket in `SSH_AUTH_SOCK` elsewhere) and left Pageant out. libssh2's `agent_connect_pageant` finds the window with `FindWindowA("Pageant", "Pageant")`; `agent_transact_pageant` writes the request into a named file mapping `PageantRequest%08x` (thread id), sends `WM_COPYDATA` with `AGENT_COPYDATA_ID` 0x804e50ba and reads the answer back from the mapping (max 8192 bytes). That is Win32 messaging: `LibraryImport` of `user32`/`kernel32` (AOT-safe) and `MemoryMappedFile`, no package. Build it as another `ISshAgentConnector` tried before the pipe on Windows only. Measure with the reference curl and a running Pageant holding a test key (`Record-CurlExchange.ps1 -NoServer`, as ADR-0270 did).

## Acceptance criteria

- [ ] Measured first: curl's agent lines and messages with Pageant running and the OpenSSH pipe also present, and with Pageant only; in Notes.
- [ ] `Curl.Protocol.Ssh.UnitTests` pin the Pageant-first order with fakes; the Win32 adapter is covered by a Windows-only Integration test or excluded under ADR-0083 with a justification.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-30: Created.
