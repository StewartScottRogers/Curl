---
id: BL-5034
title: Make the SMB documentation and curl -V agree on which builds serve smb
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [README.md, Documentation/Protocols.md, Curl.Engine.UnitLibrary/EngineComposition.cs]
requirement: none
created: 2026-11-12
completed:
---
# BL-5034 — Make the SMB documentation and curl -V agree on which builds serve smb

## Goal

An ADR decides which builds serve `smb`/`smbs`, and README.md, `Documentation/Protocols.md` and `curl -V` all say the same thing.

## Context

From the outside fitness review of 2026-11-12 (commit 2d2ad7f8b, `preview` curl-win-x64.zip; REF = curl 8.22.0 Schannel, REF2 = curl 8.18.0 OpenSSL), finding STD-039 (Low, Probable: the docs/behaviour mismatch is confirmed on Windows; the Linux build was not run).

- **Repro:** `curl -sS smb://127.0.0.1:44445/share/x; echo $?`, `curl -sS smbs://127.0.0.1:44445/share/x; echo $?`, `curl -V`.
- **Expected:** README.md (Protocols list, lines 20-23) and the SMB row of `Documentation/Protocols.md` both describe smb and smbs as served ("Downloads a file from a share, or uploads one with `-T`..."). The Linux reference family (REF2, curl 8.18 OpenSSL) lists `smb smbs` in `-V` and tries to connect.
- **Actual:** PORT prints `curl: (1) Protocol "smb" is disabled` and exits 1, and `-V` lists no smb or smbs. This matches Windows REF (Schannel build, no SMB) exactly. `Curl.Engine.UnitLibrary/EngineComposition.cs:196` registers `DisabledProtocolHandler(["smb","smbs"])` unconditionally, on every OS, so `SmbProtocolHandler` is never reached.
- **Where to start:** README.md, `Documentation/Protocols.md` (SMB row), `EngineComposition.cs:196`.

The standing rule is "match the platform's curl": the Schannel build on Windows has no SMB; the OpenSSL build on Linux and macOS lists `smb smbs`.

## Acceptance criteria

- [ ] An ADR under `Documentation/Planning/Decisions/`, marked "Decided by Claude under Stewart's delegation", records the decision: either serve SMB on the OpenSSL-profile builds (Linux, macOS) and keep it disabled on the Schannel build, or document it as disabled everywhere; with the reasoning (platform's curl, measured `curl -V` of the reference builds).
- [ ] README.md's Protocols list and the SMB row of `Documentation/Protocols.md` state exactly what the ADR decided, per platform build, and agree with what `curl -V` lists.
- [ ] If the decision is "disabled everywhere", the docs say smb/smbs are disabled in all builds and any claim that they are served is removed.
- [ ] If the decision is to serve SMB on Linux/macOS, the code change (the platform-conditional registration in `EngineComposition.cs`, `curl -V` listing `smb smbs`, `curl -sS smb://127.0.0.1:1/s/x` exiting 7 there) is filed by this run as a follow-up task with its own acceptance criteria, depended on by nothing here; this task still ships the ADR and docs, worded as intent until that task is Done.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text. Docs must be true of the code as it is now (CLAUDE.md, "Say what it does"); intent is written as intent. A new `Documentation/Planning/Decisions/` ADR file is created by this task.

## Log

- 2026-11-12: Created.
