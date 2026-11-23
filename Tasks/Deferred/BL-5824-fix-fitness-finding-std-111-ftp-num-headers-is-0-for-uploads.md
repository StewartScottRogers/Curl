---
id: BL-5824
title: Fix fitness finding STD-111: FTP %{num_headers} is 0 for uploads and for transfers that end in a data-connection timeout (STILL OPEN, partly fixed)
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ftp.UnitLibrary/FtpSession.cs]
requirement: none
created: 2026-11-23
completed:
---
# BL-5824 — Fix fitness finding STD-111: FTP %{num_headers} is 0 for uploads and for transfers that end in a data-connection timeout (STILL OPEN, partly fixed)

## Goal

STD-111 from the outside fitness review of 2026-11-23 09:23 no longer reproduces: FTP %{num_headers} is 0 for uploads and for transfers that end in a data-connection timeout (STILL OPEN, partly fixed). Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-23T0923` (commit `bef3629`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL/OpenLDAP on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

This finding was filed before (fitness review 2026-11-18T0633) and is only partly fixed; the finding says which part still reproduces.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the OpenLDAP server from STD-117"), find the task titled with that finding ID. Check that it still reproduces on the current branch first. The finding, verbatim:

- **Severity:** Low
- **Confidence:** Confirmed (2 runs each, two servers)
- **Protocol/area:** FTP write-out, Windows. Downloads, `-I` and `-Q` now match.
- **Repro:**
  1. pyftpdlib on 127.0.0.1:24021, user `x`/`p` with write permission: `curl -sS -T loc.txt -w "%{num_headers}\n" ftp://x:p@127.0.0.1:24021/nh1.txt`
  2. An FTP mock on 24030 whose replies contain colons and which accepts STOR: `curl -sS -T loc.txt -w "%{num_headers}\n" ftp://colon:p@127.0.0.1:24030/u.txt`
  3. The same mock, user `badip`, answers PASV with 10.255.255.1 and refuses EPSV: `curl -sS -m 8 --no-ftp-skip-pasv-ip -w "[%{num_headers}]\n" ftp://badip:p@127.0.0.1:24030/f.txt`
- **Expected (REF):** `1`, `5`, `[4]` (exit 28).
- **Actual (.NET):** `0`, `0`, `[0]`. The `-D -` output is byte-identical to REF's.
- **Likely location:** `Curl.Original.Protocol.Ftp.UnitLibrary/FtpSession.cs` `PseudoHeadersOf` (~line 1556): `uploaded ? [] : …` discards `control.ColonReplyLines` for uploads; the timeout path also builds the report without them.
- **Acceptance check:** the three commands print `1`, `5`, `[4]`.

## Acceptance criteria

- [ ] the three commands print `1`, `5`, `[4]`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-23: Created from fitness review 2026-11-23T0923.
- 2026-11-23: Backlog -> Deferred. Stewart 2026-11-23: no more tasks this shift; finish Doing and stop. Move back to Backlog to resume.
