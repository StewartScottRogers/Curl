---
id: BL-5048
title: Exit 60 when the --dane resolver cannot be reached
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.Dane.UnitLibrary/DaneCheckHook.cs, Curl.Extension.Dane.UnitLibrary/Lookup/DnssecQueryTransport.cs, Curl.Extension.Dane.UnitTests/DaneCheckHookTests.cs, Curl.Extension.Dane.UnitTests/Lookup/DnssecQueryTransportTests.cs, Documentation/Planning/Decisions/ADR-1156-dane-extension-checks-tlsa-records-validated-from-the-root-with-dane.md]
requirement: none
created: 2026-11-12
completed:
---
# BL-5048 — Exit 60 when the --dane resolver cannot be reached

## Goal

`--dane <resolver>` with a resolver that cannot be reached exits 60 with a message saying the TLSA lookup through that resolver got no answer, instead of exit 7.

## Context

From the outside fitness review of 2026-11-12 (commit 2d2ad7f8b, `preview` curl-win-x64.zip), finding EXT-040 (Low, Confirmed). It still fails closed, so it is not a bypass.

**Repro.** Run an HTTPS server on 127.0.0.1:48443 with a self-signed certificate for `localhost` (`tls.crt`); make sure nothing listens on 48999. Run `curl -sS --cacert tls.crt -m 10 --dane 127.0.0.1:48999 https://localhost:48443/`. Repeat with `-k` instead of `--cacert tls.crt`.

**Expected.** ADR-1156: "no usable answer (timeout, unreachable resolver, error RCODE): exit 60, failing closed".

**Actual.** `curl: (7) Failed to connect to 127.0.0.1:48999 after 0 ms: Could not connect to server`, exit 7. It reads like a failure to reach the web server.

**Where to start.** `Curl.Extension.Dane.UnitLibrary/DaneCheckHook.cs` (`RefusalOfAsync` catches only `ArgumentException`; the existing refusal text for a `Failed` lookup is `DANE: the resolver {resolver} gave no usable answer for {name}'s TLSA records`) and `Lookup/` (`DnssecQueryTransport.cs`, `TlsaLookup.cs`, `TlsaLookupStatus.Failed`). A resolver connect failure takes the connector's exit-7 path instead of becoming a `Failed` result and the hook's exit-60 refusal.

## Acceptance criteria

- [ ] `Curl.Extension.Dane.UnitTests/Lookup/DnssecQueryTransportTests.cs` or `TlsaLookupTests.cs` has a test that a resolver whose connection is refused (a connector that throws a connect failure) gives `TlsaLookupStatus.Failed`, not an exception.
- [ ] `Curl.Extension.Dane.UnitTests/DaneCheckHookTests.cs` has a test that an unreachable resolver makes `RefusalOfAsync` return a refusal naming the resolver (`127.0.0.1:48999`) and the TLSA name, so the transfer ends with exit 60 (the exit the hook's refusal already maps to), for both a trusted and a skipped-verification (`-k`) transfer.
- [ ] Regression: timeout and error-RCODE resolver answers still exit 60, and a secure matching answer still passes.
- [ ] ADR-1156 (`Documentation/Planning/Decisions/ADR-1156-dane-extension-checks-tlsa-records-validated-from-the-root-with-dane.md`) matches the behaviour (unreachable resolver exits 60).
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Unit tests use `Curl.Extension.Dane.UnitTests/Lookup/FakeDnsServer.cs` and fake connectors, no real socket. Not verified: which file throws the connect failure (hook, `DnssecQueryTransport` or the injected connector); the implementer finds it by running the hook test with a refusing connector.

## Log

- 2026-11-12: Created.
