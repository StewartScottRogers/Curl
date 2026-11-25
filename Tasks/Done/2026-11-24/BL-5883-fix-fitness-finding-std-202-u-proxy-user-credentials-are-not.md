---
id: BL-5883
title: Fix fitness finding STD-202: -U / --proxy-user credentials are not percent-decoded, so the wrong proxy credential is sent
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/TransferProxySelection.cs, Curl.Engine.UnitLibrary/TransferCredentialLookup.cs, Curl.Engine.UnitLibrary/CLAUDE.md, Curl.Engine.UnitLibrary/ManagedEasyTransfer.Preparation.cs, Curl.Engine.UnitTests/Std202Tests.cs, Curl.Engine.UnitTests/EasyProxyOptionsReadingTests.cs]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-5883 — Fix fitness finding STD-202: -U / --proxy-user credentials are not percent-decoded, so the wrong proxy credential is sent

## Goal

STD-202 from the outside fitness review of 2026-11-24 13:58 no longer reproduces: -U / --proxy-user credentials are not percent-decoded, so the wrong proxy credential is sent. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-24T1358` (commit `1e5d591`, the `preview` release binaries). That review was a deeper second pass of the same commit as `Curl-Fitness-Review-2026-11-24T0708`, covering areas the earlier pass did not test.

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer's mock servers were Python, and some findings used real servers in Docker (Dovecot, GreenMail, Samba, Mosquitto, Kafka, Prosody, SeaweedFS, Azurite); rebuild any mock you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Check that it still reproduces on the current branch first.

The finding, verbatim:

- **Severity:** Medium (silent wrong operation: authentication to the proxy fails or uses the wrong identity) · **Confidence:** Confirmed (2 runs Linux, 1 run Windows) · **Area:** HTTP proxy authentication. Both platforms.
- **Mock:** an HTTP proxy on 127.0.0.1:32090 that echoes the request it received (any echoing proxy works).
- **Repro:**
  - `curl -sS -x http://127.0.0.1:32090 -U "u%3Ax:p%40ss" http://127.0.0.1:32080/plain/a`
  - `curl -sS -x http://127.0.0.1:32090 -U "us%20er:p%40ss" http://127.0.0.1:32080/plain/a`
- **Expected (REF 8.22 Windows and REF2 8.18):** `Proxy-Authorization: Basic dTp4OnBAc3M=` (decodes to `u:x:p@ss`), and for the second form `Basic dXMgZXI6cEBzcw==` (`us er:p@ss`). libcurl URL-decodes the proxy user name and password given with `-U`; this is the only way to put `:` in a proxy user name.
- **Actual (.NET):** `Proxy-Authorization: Basic dSUzQXg6cCU0MHNz`, the literal `u%3Ax:p%40ss`, exit 0.
- **Scope:** `-u` (server credentials) is not decoded by curl either, and the port matches. Credentials inside the `-x` URL are decoded correctly by the port.
- **Likely location:** `Curl.Engine.UnitLibrary/TransferProxySelection.cs`, `WithProxyUser(...)` applies the `-U` `NetworkCredential` verbatim. Percent-decode user and password first, as for proxy-URL userinfo; the same applies to SOCKS5 user/password from `-U`.
- **Acceptance check:** `curl -sS -x http://127.0.0.1:32090 -U "u%3Ax:p%40ss" http://127.0.0.1:32080/plain/a` sends `Proxy-Authorization: Basic dTp4OnBAc3M=`.

## Acceptance criteria

- [x] `curl -sS -x http://127.0.0.1:32090 -U "u%3Ax:p%40ss" http://127.0.0.1:32080/plain/a` sends `Proxy-Authorization: Basic dTp4OnBAc3M=`.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Done directly rather than through the `/feature` subagent stages: a one-method fix, and the run's cost cap is small.
- Measured on real curl 8.22.0 (Windows, `Record-CurlExchange.ps1`): `-U u%3Ax:p%40ss` sends `Basic dTp4OnBAc3M=`; `%zz`, `%4` and a lone `%` stay as they are; `%ff%C3%A9:%e9` sends those raw bytes; a `%00` in either half is exit 3 `URL using bad/illegal format or missing URL` for every transfer (proxy, no proxy, `file://`, `--noproxy`, URL with its own proxy credential, unusable `-x`). Curl matches the bytes, exit code and `-v` user line (checked end to end).
- Decided (Claude, Stewart's delegation; no ADR, as it only matches curl): the library path stays undecoded. Upstream test503, on the ratchet, sends `CURLOPT_PROXYUSERPWD` `test%20:ing%41` literally, so `ManagedEasyTransfer` asks `TrySelect` to decode only under `ToolSettings`.
- Touches widened to `TransferCredentialLookup.cs` (its byte-level decoder made internal and reused), `ManagedEasyTransfer.Preparation.cs`, the Engine `CLAUDE.md`, `Std202Tests.cs` and `EasyProxyOptionsReadingTests.cs` (it asserted the same credential instance). No other task in Doing names any of them.
- Build clean, fast tests green, `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary`: 0 failing members.

## Log

- 2026-11-24: Created from fitness review 2026-11-24T1358.
- 2026-11-24: Backlog -> Doing. Criteria at claim: f2ace235 69116afe 3d836d5e.
- 2026-11-24: Doing -> Done. -U / --proxy-user is percent-decoded as curl 8.22.0 does; a decoded NUL is exit 3
