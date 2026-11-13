---
id: BL-5134
title: Refuse --tlsv1.3 with a --ciphers list on Schannel with exit 35
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/SchannelAlgorithmList.cs, Curl.Console/EasyOptionsFromCommandLine.cs, Curl.Networking.UnitTests/SchannelTls13CipherListRefusalTests.cs]
requirement: none
created: 2026-11-13
completed:
---
# BL-5134 — Refuse --tlsv1.3 with a --ciphers list on Schannel with exit 35

## Goal

On Windows (Schannel), `--tlsv1.3` together with `--ciphers <list>` is refused with exit 35 and curl's message, before any handshake, instead of connecting with TLS 1.3 and silently ignoring the list.

## Context

From the outside fitness review of 2026-11-13 (commit 56cb97ede, `preview` build), finding STD-057 (Low, Confirmed twice). The review also lists it as a "before drop-in" item, hence Normal priority. A cipher restriction the user asked for is silently not applied. Area: TLS options, Windows.

**Repro.** With any TLS 1.3-capable HTTPS server on 127.0.0.1:22100 using a valid localhost certificate: `curl -sS https://localhost:22100/ --cacert ca.pem --ssl-no-revoke --tlsv1.3 --ciphers CALG_AES_256` (same without `--ssl-no-revoke`, and with `-k`).

**Expected (upstream curl, Schannel).** `curl: (35) schannel: TLS 1.3 is not supported with a cipher list; remove the cipher list or allow a lower TLS version`, exit 35, no request sent. Without `--tlsv1.3`, `--ciphers CALG_AES_256` succeeds in both.

**Actual.** Exit 0, body delivered, server logs TLSv1.3.

**Where to start.** `Curl.Networking.UnitLibrary/SchannelAlgorithmList.cs` and the TLS option setup fed by `Curl.Console/EasyOptionsFromCommandLine.cs` (about line 342, `CipherList`). When the minimum TLS version is 1.3 and a cipher list is set, refuse with exit 35 (CURLE_SSL_CONNECT_ERROR) and the message above. Keep it on the Schannel route only; the OpenSSL-style route on Linux and macOS is not changed by this task.

## Acceptance criteria

- [ ] New tests in `Curl.Networking.UnitTests/SchannelTls13CipherListRefusalTests.cs` prove that minimum version TLS 1.3 plus a non-empty cipher list yields exit 35 with the exact message `schannel: TLS 1.3 is not supported with a cipher list; remove the cipher list or allow a lower TLS version`, before any connection is opened (a connection stand-in records zero opens).
- [ ] Regression tests prove that a cipher list without `--tlsv1.3`, and `--tlsv1.3` without a cipher list, are not refused.
- [ ] Tests are platform-neutral, or marked `[OSCondition(OperatingSystems.Windows)]` where they depend on the Schannel route.
- [ ] No option is added, so `--ai-help` and `--help` need no change; the task's Log states this was checked.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check, as a hand check: the repro command exits 35 with the message above and the server logs no handshake.

## Log

- 2026-11-13: Created.
