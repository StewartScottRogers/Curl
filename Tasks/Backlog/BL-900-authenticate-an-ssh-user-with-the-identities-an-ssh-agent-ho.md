---
id: BL-900
title: Authenticate an SSH user with the identities an ssh-agent holds
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-567, BL-568]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-900 — Authenticate an SSH user with the identities an ssh-agent holds

## Goal

When the server's method list (the answer to `none`) names `publickey`, curl 8.21.0 tries the identities of a running ssh-agent after `password` and before `keyboard-interactive` (libcurl's SSH_AUTH_AGENT_INIT / SSH_AUTH_AGENT states: libssh2_agent_init, connect, list identities, try each with libssh2_agent_userauth). Build that step in `Curl.Protocol.Ssh.UnitLibrary`'s `Authentication.SshUserAuthentication`: the agent protocol (draft-miller-ssh-agent: SSH_AGENTC_REQUEST_IDENTITIES, SSH2_AGENTC_SIGN_REQUEST) behind an injected seam so tests use a fake agent; on Windows the agent is the OpenSSH named pipe `\\.\pipe\openssh-ssh-agent` (and Pageant where libssh2 1.11.1 uses it), elsewhere the Unix socket in `SSH_AUTH_SOCK`. An agent that cannot be reached is skipped silently and curl goes on to keyboard-interactive.

## Context

BL-567 (ADR-0214) built none/password/keyboard-interactive and measured that with no agent reachable curl sends no publickey request; BL-568 builds publickey from key files. No existing task covers the agent. BCL only: named pipes (`System.IO.Pipes`) and Unix domain sockets are in the BCL; never a package. Measure with the reference curl (`Record-CurlExchange.ps1 -NoServer`) against a loopback server built from the library's classes, as ADR-0214 did, with a real ssh-agent holding a test key (Windows OpenSSH ssh-agent service or `ssh-agent` in WSL), recording the userauth messages curl sends.

## Acceptance criteria

- [ ] Measured first: the publickey requests curl sends from an agent with one and with two identities, a server that refuses them, and no agent running; stderr and exit code of each in Notes.
- [ ] `Curl.Protocol.Ssh.UnitTests` pin the agent requests and the userauth messages against a fake agent and the in-memory peer, including the method order password -> agent -> keyboard-interactive.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
