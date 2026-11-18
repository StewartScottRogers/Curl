---
id: BL-5406
title: Make the remaining extension option errors command-line errors without .NET text and refuse bad DoT and DoQ resolvers
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.WireGuard.UnitLibrary/WireGuardExtension.cs, Curl.Extension.WireGuard.UnitTests, Curl.Extension.CertificateTransparency.UnitLibrary/CertificateTransparencyCheckHook.cs, Curl.Extension.CertificateTransparency.UnitTests, Curl.Extension.Sigstore.UnitLibrary/SigstoreTransferHook.cs, Curl.Extension.Sigstore.UnitTests, Curl.Extension.OAuth2.UnitLibrary/Options/OAuth2Configuration.cs, Curl.Extension.OAuth2.UnitTests, Curl.Extension.HttpFile.UnitLibrary/HttpFileArgumentExpander.cs, Curl.Extension.HttpFile.UnitTests, Curl.Extension.OpenApi.UnitLibrary/OpenApiArgumentExpander.cs, Curl.Extension.OpenApi.UnitTests, Curl.Extension.EncryptedDns.UnitLibrary/EncryptedDnsExtension.cs, Curl.Extension.EncryptedDns.UnitLibrary/DnsOverTls, Curl.Extension.EncryptedDns.UnitTests]
requirement: none
created: 2026-11-17
completed:
---
# BL-5406 — Make the remaining extension option errors command-line errors without .NET text and refuse bad DoT and DoQ resolvers

## Goal

Every extension option error left in the review is a command-line error in one style (`curl: <message>`, the try-help line, exit 2) with no .NET exception text or full local path, and `--dns-over-tls` / `--dns-over-quic` refuse a value that is not host[:port] or a bare IPv6 address.

## Context

From the outside fitness review of 2026-11-17 (commit 9561bbe, `preview` build), finding EXT-079 (Low, message and validation only, Confirmed). Area: extension option validation, Windows. Follows ADR-1289 (an extension option refused at activation is a command-line error that never carries .NET exception text) and ADR-1070 (a bad `--dns-over-tls`/`--dns-over-quic` value is a command-line error naming the option, exit 2).

**Repro** (any HTTP server on 127.0.0.1:43001).
1. `curl -sS --wireguard nonexist.conf http://127.0.0.1:43001/plain`
2. `curl -sS --require-sct nonexist.json --cacert ca.pem --ssl-no-revoke https://localhost:43006/plain`
3. `curl -sS --sigstore-identity me http://127.0.0.1:43001/plain`; `curl -sS --oauth2-device x http://127.0.0.1:43001/plain`
4. `curl -sS --http-file nonexist.http`; `curl -sS --openapi nonexist.yaml --operation x http://127.0.0.1:43001/plain`
5. `curl -sS --dns-over-tls "::bad::" http://localhost:43001/plain` (also `"a b"`, `-5`, and `--dns-over-quic`)
**Expected.** ADR-1289 and ADR-1070 as above.
**Actual (PORT).**
1. `curl: --wireguard cannot read 'nonexist.conf': Could not find file 'C:\Users\<user>\...\nonexist.conf'.` (with `-5`: `Access to the path '...\-5' is denied.`), exit 2.
2. `curl: (60) Certificate Transparency: the log list nonexist.json could not be read: Could not find file 'C:\Users\<user>\...\nonexist.json'.`
3. `curl: (2) --sigstore-identity needs --sigstore-bundle` and `curl: (2) --oauth2-device needs --oauth2-client-id.`: the `(2)` form with no try-help line, unlike `--ohttp-keys`, `--npipe`, `--odoh-proxy`.
4. `curl: http-file: cannot read nonexist.http` / `curl: openapi: cannot read nonexist.yaml`, exit 26 with no `(26)` and a different prefix style.
5. `curl: (6) Could not resolve host: localhost`, exit 6; the bad resolver value is accepted. (`host:notaport`, `127.0.0.1:99999` and empty are refused with exit 2 as documented.)
**Where to start.** `Curl.Extension.WireGuard.UnitLibrary` config reader and `Curl.Extension.CertificateTransparency.UnitLibrary` log-list reader (they use the `IOException.Message` as is); the activation checks in `Curl.Extension.Sigstore.UnitLibrary` and `Curl.Extension.OAuth2.UnitLibrary` (the check runs at transfer time and gives `(2)`); `Curl.Extension.EncryptedDns.UnitLibrary` resolver parser (treats any value with `:` as IPv6). The `--http-file` and `--openapi` messages come from `Curl.Extension.HttpFile.UnitLibrary/HttpFileArgumentExpander.cs` and `Curl.Extension.OpenApi.UnitLibrary/OpenApiArgumentExpander.cs`; the resolver check is in `EncryptedDnsExtension.cs` and `DnsOverTls/DnsOverTlsResolverHook.cs`.

## Acceptance criteria

- [ ] New tests in `Curl.Extension.WireGuard.UnitTests` prove a missing `--wireguard` file gives `--wireguard cannot read 'nonexist.conf'` as a command-line error (exit 2) whose text contains neither a directory path nor .NET exception wording.
- [ ] New tests in `Curl.Extension.CertificateTransparency.UnitTests` prove a missing `--require-sct` log list gives a message naming the option and the file as given, with no full path or .NET text.
- [ ] New tests in `Curl.Extension.Sigstore.UnitTests` and `Curl.Extension.OAuth2.UnitTests` prove `--sigstore-identity` without `--sigstore-bundle` and `--oauth2-device` without `--oauth2-client-id` are refused at activation as command-line errors (message, try-help line, exit 2).
- [ ] The unreadable `--http-file` and `--openapi` cases give a command-line error in the same style (tests in each extension's `.UnitTests`), with no `(26)` mismatch.
- [ ] New tests in `Curl.Extension.EncryptedDns.UnitTests` prove `"::bad::"`, `"a b"` and `-5` are refused for both `--dns-over-tls` and `--dns-over-quic` with exit 2 and a message naming the option and the value, and that `host`, `host:853`, `[::1]:853` and `::1` are still accepted.
- [ ] Tests are platform-neutral (no assertion on a Windows path or Windows error text).
- [ ] Hand check recorded in the Log: command 1 prints `curl: --wireguard cannot read 'nonexist.conf'` plus the try-help line, exit 2, with no .NET text or path; command 3 prints `curl: --sigstore-identity needs --sigstore-bundle` plus the try-help line, exit 2; command 5 exits 2 with a message naming `--dns-over-tls` and `'::bad::'`.
- [ ] `--extended-help` and `--ai-help` are checked and kept right if they quote any changed message.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- One task, five libraries by design: the change is one message style. If it grows past one run, split per library.
- `touches` lists the exact files found by grep on 2026-11-17 (the `--http-file` and `--openapi` expanders are included); a fix may need a neighbouring file in the same library, which is fine, but adding a new library to `touches` means the task has outgrown one run.
- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check: command 1 prints `curl: --wireguard cannot read 'nonexist.conf'` plus the try-help line, exit 2, with no .NET text or path; command 3 prints `curl: --sigstore-identity needs --sigstore-bundle` plus the try-help line, exit 2; command 5 exits 2 with a message naming `--dns-over-tls` and `'::bad::'`.

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing. Criteria at claim: a4c06614 9483f70b e7d0030b 39d6f7d1 2b23cfda b5f75c39 8a1d8b43 5a95a163 a0b49a29.
