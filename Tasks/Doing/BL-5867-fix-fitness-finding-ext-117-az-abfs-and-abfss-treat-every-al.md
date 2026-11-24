---
id: BL-5867
title: Fix fitness finding EXT-117: az, abfs and abfss treat every all-digit or 0x... account name as an emulator host and send the token to that bare host
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.AzureStorage.UnitLibrary/Requests/AzureStorageUrlMapping.cs]
requirement: none
created: 2026-11-24
completed:
---
# BL-5867 — Fix fitness finding EXT-117: az, abfs and abfss treat every all-digit or 0x... account name as an emulator host and send the token to that bare host

## Goal

EXT-117 from the outside fitness review of 2026-11-24 07:08 no longer reproduces: az, abfs and abfss treat every all-digit or 0x... account name as an emulator host and send the token to that bare host. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-24T0708` (commit `1e5d591`, the `preview` release binaries).

This is an extension (Part B): it is judged against Curl's own documentation (`curl --extended-help`, `Documentation/Protocols.md`, `Documentation/Conformance/Extended/*.md`, the ADRs), never against C curl.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup, find the task titled with that finding ID. Check that it still reproduces on the current branch first. The finding, verbatim:


- **Severity:** Medium (the token goes to whatever the bare name resolves to, e.g. via a DNS search suffix; a valid account never reaches Azure) · **Confidence:** Confirmed (2 runs) · **Area:** Azure Storage endpoint mapping. Regression from this round's EXT-105 fix.
- **Mock:** an HTTPS server on 127.0.0.1:27010 logging request heads.
- **Repro:**
  - `curl -sS -k --oauth2-bearer TOK --connect-to "::127.0.0.1:27010" "abfss://fs@0xstore/x"` → check the logged `Host`.
  - Same with `az://0xstore/ctr/b`, `az://12345678/ctr/b`, `abfss://fs@12345678/x`.
  - Without `--connect-to`: `curl -sS --oauth2-bearer TOK "az://0xstore/ctr/b"`.
  - Control: `az://acct1/ctr/b` → `Host: acct1.blob.core.windows.net`.
- **Expected:** `--extended-help` (azure-storage) uses emulator (path-style) addressing only "at an IP address or localhost". Azure account names are 3–24 lowercase letters and digits, so `0xstore`, `12345678` and `123` are valid account names (`0xstore` is not an IPv4 spelling at all). They should map to `0xstore.dfs.core.windows.net` / `12345678.blob.core.windows.net`.
- **Actual (.NET):** `Host: 0xstore` / `Host: 12345678` with `Authorization: Bearer TOK`. az URLs are re-read as account `ctr`, container `b` (`GET /ctr/b?restype=container&comp=list…`, exit 8). Without `--connect-to`: `curl: (6) Could not resolve host: 0xstore`.
- **Likely location:** `Curl.Extended.Protocol.AzureStorage.UnitLibrary/Requests/AzureStorageUrlMapping.cs`, `IsNumericIpv4Spelling()`: `StartsWith("0x")` or all-digits-and-dots is too broad. Use the port's own IPv4 host parser (at most 4 parts, valid hex/octal/decimal digits, values in range).
- **Acceptance check:** `abfss://fs@0xstore/x` logs `Host: 0xstore.dfs.core.windows.net`; `az://12345678/ctr/b` logs `Host: 12345678.blob.core.windows.net`; `abfss://fs@0x7f000001/x` still logs `Host: 0x7f000001`.

## Acceptance criteria

- [ ] `abfss://fs@0xstore/x` logs `Host: 0xstore.dfs.core.windows.net`; `az://12345678/ctr/b` logs `Host: 12345678.blob.core.windows.net`; `abfss://fs@0x7f000001/x` still logs `Host: 0x7f000001`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-24: Created from fitness review 2026-11-24T0708.
- 2026-11-24: Backlog -> Doing. Criteria at claim: 70ec4bdf 69116afe 3d836d5e.
