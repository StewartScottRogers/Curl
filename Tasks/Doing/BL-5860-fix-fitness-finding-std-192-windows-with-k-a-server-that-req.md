---
id: BL-5860
title: Fix fitness finding STD-192: Windows: with -k, a server that requires a client certificate gives a different exit-56 message
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/TlsFailureMessages.cs]
requirement: none
created: 2026-11-24
completed:
---
# BL-5860 — Fix fitness finding STD-192: Windows: with -k, a server that requires a client certificate gives a different exit-56 message

## Goal

STD-192 from the outside fitness review of 2026-11-24 07:08 no longer reproduces: Windows: with -k, a server that requires a client certificate gives a different exit-56 message. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-24T0708` (commit `1e5d591`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL/OpenLDAP on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup, find the task titled with that finding ID. Check that it still reproduces on the current branch first. The finding, verbatim:


- **Severity:** Low (message only) · **Confidence:** Probable (one run) · **Area:** Schannel TLS.
- **Setup:** a TLS server that requires a client certificate (`ssl.CERT_REQUIRED`); none is sent.
- **Repro:** `curl -sS -k https://localhost:<p>/`
- **Expected (REF):** `curl: (56) schannel: failed to read data from server: SEC_E_ILLEGAL_MESSAGE (0x80090326) - This error usually occurs when a fatal SSL/TLS alert is received (e.g. handshake failed). …`
- **Actual (.NET):** `curl: (56) Recv failure: Connection was aborted`.
- **Acceptance check:** the port prints the schannel `SEC_E_ILLEGAL_MESSAGE` text, exit 56.

## Acceptance criteria

- [ ] the port prints the schannel `SEC_E_ILLEGAL_MESSAGE` text, exit 56.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-24: Created from fitness review 2026-11-24T0708.
- 2026-11-24: Backlog -> Doing. Criteria at claim: 63a08c31 69116afe 3d836d5e.
