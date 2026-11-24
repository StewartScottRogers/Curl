---
id: BL-5894
title: Fix fitness finding STD-213: WebSocket: on a protocol error (exit 56), %{size_download} counts the rejected frame's bytes; curl reports 0
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.WebSocket.UnitLibrary/WsFrameDecoder.cs, Curl.WebSocket.UnitLibrary/WsDecodedFrames.cs]
requirement: none
created: 2026-11-24
completed:
---
# BL-5894 — Fix fitness finding STD-213: WebSocket: on a protocol error (exit 56), %{size_download} counts the rejected frame's bytes; curl reports 0

## Goal

STD-213 from the outside fitness review of 2026-11-24 13:58 no longer reproduces: WebSocket: on a protocol error (exit 56), %{size_download} counts the rejected frame's bytes; curl reports 0. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-24T1358` (commit `1e5d591`, the `preview` release binaries). That review was a deeper second pass of the same commit as `Curl-Fitness-Review-2026-11-24T0708`, covering areas the earlier pass did not test.

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer's mock servers were Python, and some findings used real servers in Docker (Dovecot, GreenMail, Samba, Mosquitto, Kafka, Prosody, SeaweedFS, Azurite); rebuild any mock you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Check that it still reproduces on the current branch first.

The finding, verbatim:

- **Severity:** Low · **Confidence:** Confirmed (2 runs, 9 error kinds) · **Area:** ws / wss, Windows build.
- **Mock:** a WebSocket server on 127.0.0.1:36140 whose path picks the bad frame: `/masked` (a masked server frame), `/rsv` (RSV1 set), `/badop` (opcode 3), `/huge` (2^63 length), `/ctrlfrag` (non-FIN ping), `/ctrlbig` (200-byte ping), `/cont` (orphan continuation), `/interleave` (interrupted fragmented message).
- **Repro:** `curl -sS -w '[%{exitcode} %{size_download}]' ws://127.0.0.1:36140/masked`, and the same for the other paths.
- **Expected (REF):** `[56 0]` with `curl: (56) [WS] masked input frame` (or the matching message) for every path.
- **Actual (.NET):** the same message and exit 56, but `size_download` is 18, 10, 7, 14, 14, 8, 209, 10 and 13 for `/masked`, `/rsv`, `/badop`, `/huge`, a second huge-length variant, `/ctrlfrag`, `/ctrlbig`, `/cont` and `/interleave` — the bytes of the rejected frame. Error text, stdout and the `-o` file match.
- **Likely location:** `Curl.WebSocket.UnitLibrary/WsFrameDecoder.cs` / `WsDecodedFrames.cs`: bytes are added to the download size before the frame is validated.
- **Acceptance check:** `curl -sS -w '[%{exitcode} %{size_download}]' ws://127.0.0.1:36140/masked` prints `[56 0]`.

## Acceptance criteria

- [ ] `curl -sS -w '[%{exitcode} %{size_download}]' ws://127.0.0.1:36140/masked` prints `[56 0]`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-24: Created from fitness review 2026-11-24T1358.
