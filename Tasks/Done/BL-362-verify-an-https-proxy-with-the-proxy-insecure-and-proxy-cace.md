---
id: BL-362
title: Verify an HTTPS proxy with the --proxy-insecure and --proxy-cacert family
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-266]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-362 — Verify an HTTPS proxy with the --proxy-insecure and --proxy-cacert family

## Goal

`curl --proxy-insecure -x https://<proxy> <url>` skips verification of the proxy's certificate and `--proxy-cacert <file>` trusts only that file for it, while `-k` and `--cacert` keep applying only to the target, as curl 8.21.0 does.

## Context

- Filed by BL-266 (2026-09-27), which made `TcpConnector` run the handshake to a `ProxyKind.Https` proxy through the same injected `ITlsProvider` as the target's (ADR-0061). Give `TcpConnector` a separate proxy `ITlsProvider` (e.g. an optional `proxyTlsProvider` constructor parameter defaulting to `tlsProvider`) in `Curl.Networking.UnitLibrary`; note `CurlCompositionTests.CapturedDependency<ITlsProvider>` in `Curl.Console.UnitTests` expects exactly one `ITlsProvider` field on `TcpConnector`, so adjust that test in the same change.
- Measured in BL-266's Notes: `curl -s -S -k -x https://localhost:18404 https://example.com/` against a self-signed proxy fails with exit 60 `schannel: SEC_E_UNTRUSTED_ROOT ...`, so `-k` does not reach the proxy; `--proxy-insecure` makes the same run reach CONNECT.
- Parse `--proxy-insecure`, `--proxy-cacert`, `--proxy-capath` (and whichever of the `--proxy-*` TLS family the reference build accepts) in `Curl.Cli.UnitLibrary`; in `Curl.Console`'s composition build a second `SslStreamTlsProvider` from a `TlsClientOptions` made of those options and pass it as `proxyTlsProvider`.
- Upstream: https://curl.se/docs/manpage.html (`--proxy-insecure`, `--proxy-cacert`, `--proxy-capath`). Measure with `/mingw64/bin/curl` (curl 8.21.0, ADR-0009) against a TLS loopback proxy and record the commands in Notes before pinning anything.

## Acceptance criteria

- [x] `--proxy-insecure` and `--proxy-cacert <file>` parse, and a named test in `Curl.Console.UnitTests` shows the composition builds `TcpConnector` with a proxy TLS provider made from them, not from `-k`/`--cacert`.
- [x] Without any `--proxy-*` TLS option, the proxy handshake verifies against the system store even when `-k` is given (measured exit 60 above), pinned by a named test.
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for every member this task adds or changes.

## Notes

- 2026-09-27, measured with curl 8.21.0 (`C:\Program Files\Git\mingw64\bin\curl.exe`, Schannel) against a throwaway TLS loopback proxy (a C# file-based app in `C:\Temp\bl362`, outside the repository; self-signed `CN=localhost`, answers CONNECT with 407), `curl -s -S <opts> -x https://localhost:18462 https://example.com/`:
  - `-k` -> exit 60 `schannel: SEC_E_UNTRUSTED_ROOT (0x80090325) ...`; `--cacert proxy.pem` -> the same exit 60; `--proxy-capath <dir>`, `--capath <dir>` -> exit 60; `--proxy-insecure --no-proxy-insecure` -> exit 60.
  - `--proxy-cacert proxy.pem` -> `curl: (7) CONNECT tunnel failed, response 407`; `--proxy-insecure` -> the same.
  - Without `-s`: `--capath`, `--proxy-capath` or both print one `Warning: ignoring setting the CA path for the proxy, not supported by libcurl with Schannel` (wrapped at 79), with an HTTPS proxy and with none.
  - `curl --proxy-cacert nosuch.pem ...` and `--proxy-cacert ''` -> exit 2, `The file '...' provided to --proxy-cacert does not exist` / `option --proxy-cacert: is badly used here` / try-help. `--proxy-capath ''` -> `blank argument where content is expected`; `--proxy-capath -x` -> filename-looks-like-a-flag warning. `--no-proxy-cacert` -> cannot be reversed (the alias table already says so).
- Delivered: `CommandLineOptions.ProxyInsecure`/`ProxyCaCertificateFile`/`ProxyCaCertificateDirectory` (the `--cacert` existence check generalised to `SettingCaCertificateFile(longOption, set)`); `TcpConnector`'s optional `proxyTlsProvider` (defaults to `tlsProvider`) runs the HTTPS-proxy handshake; `TlsClientOptionsMapping.ProxyFromCommandLine` and a second `SslStreamTlsProvider` in `CurlTransports` (`ProxyTlsClientOptions`, `ProxyTlsProvider`); the before-transfer warnings are now the proxy provider's (curl's one CA-path warning is about the proxy and fires for `--capath` or `--proxy-capath`).
- Decisions (ADR-0094, supersedes ADR-0061 item 2): only the three verifying options are parsed - the rest of the `--proxy-*` TLS family (`--proxy-cert`, `--proxy-key`, `--proxy-ciphers`, `--proxy-tlsv1`, ...) present or negotiate rather than verify and are left for their own tasks; the proxy's CA path falls back to `--capath` as curl's tool does; everything else on the proxy handshake is the default.
- Added `Documentation/Planning/Decisions` to `touches` for ADR-0094, its README row and ADR-0061's status line; no task in Doing names it.
- `CurlCompositionTests` gained `CapturedDependency<T>(owner, fieldNameFragment)` to tell `TcpConnector`'s two `ITlsProvider` fields apart (`<tlsProvider>P` and `_proxyTlsProvider`).
- End to end is still cut off by the console's exit 4 for an HTTPS proxy with an `https` target (BL-328); the composition is ready for it.
- Gates: `dotnet build -warnaserror` 0 warnings; fast tests green in all 16 test assemblies (Cli 1979 passed, Networking 682, Console 861); `dotnet format --verify-no-changes` clean on every changed file. `Measure-CodeQuality.ps1`: Curl.Cli.UnitLibrary 100/100 with 0 failing; Curl.Networking.UnitLibrary 100/100, its 1 failing member (`SslStreamTlsProvider.VerifyPeer`, complexity 12) predates and is untouched; Curl.Console's 1 failing member (`DiskWriteOutFileOpener.TryOpen`, disk-only) predates and is untouched. Every member this task adds or changes is at 100% line and branch.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. --proxy-insecure, --proxy-cacert and --proxy-capath verify an HTTPS proxy through its own TLS provider; -k and --cacert reach only the target
