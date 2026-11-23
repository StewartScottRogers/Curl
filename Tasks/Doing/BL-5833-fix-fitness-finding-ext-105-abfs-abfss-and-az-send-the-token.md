---
id: BL-5833
title: Fix fitness finding EXT-105: abfs, abfss and az send the token to the public cloud host 0x7f000001....core.windows.net / 2130706433...., although the port reads those hosts as 127.0.0.1 for HTTP
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.AzureStorage.UnitLibrary/Requests/AzureStorageUrlMapping.cs]
requirement: none
created: 2026-11-23
completed:
---
# BL-5833 — Fix fitness finding EXT-105: abfs, abfss and az send the token to the public cloud host 0x7f000001....core.windows.net / 2130706433...., although the port reads those hosts as 127.0.0.1 for HTTP

## Goal

EXT-105 from the outside fitness review of 2026-11-23 09:23 no longer reproduces: abfs, abfss and az send the token to the public cloud host 0x7f000001....core.windows.net / 2130706433...., although the port reads those hosts as 127.0.0.1 for HTTP. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-23T0923` (commit `bef3629`, the `preview` release binaries).

This is an extension (Part B): it is judged against Curl's own documentation (`curl --extended-help`, `Documentation/Protocols.md`, `Documentation/Conformance/Extended/*.md`, the ADRs), never against C curl.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the OpenLDAP server from STD-117"), find the task titled with that finding ID. Check that it still reproduces on the current branch first.

Shared setup: Common mock used by EXT-103 to EXT-111: a Python HTTP server `srv.py PORT LOGFILE [tls]` that logs every request head (request line and all header fields) to LOGFILE; in `tls` mode it uses a certificate for 127.0.0.1/localhost signed by a test CA `ca.pem`. Paths: `POST /tus?loc=<url-encoded URL>` answers `201 Created` with `Location: <that URL>` and `Tus-Resumable: 1.0.0`; `PATCH /files/1` answers 204; `/go?to=<URL>` answers 302 to that URL; `/plain` answers 200 with `Cache-Control: max-age=60`. The finding, verbatim:

- **Severity:** Medium (bearer token sent to `<name>.dfs|blob.core.windows.net`; both names are valid, registrable storage-account names, though they do not resolve today)
- **Confidence:** Confirmed (Windows and Linux)
- **Protocol/area:** Azure Storage endpoint mapping (neighbour of the fixed EXT-085)
- **Repro:**
  1. `srv.py 27010 tls.log tls`
  2. `curl -sS -k -v --oauth2-bearer FAKETOK --connect-to "::127.0.0.1:27010" "abfss://fs@0x7f000001/x"`
  3. The same with `abfs://fs@2130706433/x` and `az://0x7f000001/acct/ctr/b`.
  4. Control: `curl -sS -v http://0x7f000001:27020/plain` shows `Trying 127.0.0.1:27020`.
- **Expected:** `--extended-help` (azure-storage) and ADR-1291: an emulator host given as an IP address or `localhost` is used as given. The port's URL parser treats `0x7f000001` and `2130706433` as 127.0.0.1 (step 4), so they must be used as given too (or refused with exit 3), never suffixed with `.core.windows.net`.
- **Actual (.NET):** `> Host: 0x7f000001.dfs.core.windows.net` with `Authorization: Bearer FAKETOK` delivered (via `--connect-to`); likewise `2130706433.dfs.core.windows.net` and `0x7f000001.blob.core.windows.net`. Linux without `--connect-to`: `Failed to connect to 0x7f000001.dfs.core.windows.net:443`.
- **Likely location:** `Curl.Extended.Protocol.AzureStorage.UnitLibrary/Requests/AzureStorageUrlMapping.cs` `IsPathStyleHost` relies on `Uri.HostNameType`, which does not recognise hex or decimal IPv4. Use the port's own host normaliser, or refuse all-digit and `0x` hosts.
- **Acceptance check:** step 2 logs `Host: 0x7f000001` or `Host: 127.0.0.1` (or exits 3) and never `.dfs.core.windows.net`; `abfss://fs@acct/x` still maps to `acct.dfs.core.windows.net`.

## Acceptance criteria

- [x] step 2 logs `Host: 0x7f000001` or `Host: 127.0.0.1` (or exits 3) and never `.dfs.core.windows.net`; `abfss://fs@acct/x` still maps to `acct.dfs.core.windows.net`.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- IsPathStyleHost now also treats hex (0x...) and all-digit/dot hosts as emulator hosts, used as given (no Azure suffix). Tests added in AzureStorageUrlMappingTests; quality gate clean. CI platform-neutral.

## Log

- 2026-11-23: Created from fitness review 2026-11-23T0923.
- 2026-11-23: Backlog -> Doing. Criteria at claim: ca07b771 69116afe 3d836d5e.
