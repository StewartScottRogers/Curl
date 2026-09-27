---
id: BL-214
title: Apply --resolve and --connect-to overrides in the resolver and connector
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-202]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-214 — Apply --resolve and --connect-to overrides in the resolver and connector

## Goal

The resolver honours `--resolve` entries and the connector honours `--connect-to` mappings.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item N4. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- BL-202 parses them; BL-244 composes them in `Curl.Console`.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] Tests show an overridden host resolves to the given address and a `--connect-to` mapping changes the dialled host and port but not the Host header input.
- [x] `dotnet build Curl.Networking.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Networking`.

## Notes

- From BL-202 (2026-09-26): the parser keeps every `--resolve` / `--connect-to` value verbatim in `CommandLineOptions.ResolveEntries` / `ConnectToEntries` and never refuses one. Measured on curl 8.21.0: `--resolve garbage`, `a:x:1.2.3.4` and `a:80:` fail at transfer time with exit 49 and `curl: (49) Could not parse CURLOPT_RESOLVE entry 'garbage'`; `''`, `*:80:…`, `+a:80:…`, `-a:80`, `[::1]:80:…` and `a:80:127.0.0.1,[::1]` are accepted; `--connect-to ''` and `garbage` are accepted and the transfer goes on. Syntax checking belongs here (or in BL-244).

- Plan item: N4 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Delivered (2026-09-27): `ResolveOverrides` (parsed from the verbatim `--resolve` values), `ConnectToMappings` (verbatim `--connect-to` values) and `ConnectDestination` in `Curl.Networking.UnitLibrary`; `TcpConnector` takes both as optional constructor parameters. It maps the target first, fails with exit 49 on a `--resolve` parse error or a matching mapping that does not parse, then resolves the mapped host and port through `ResolveOverrides.Find` before `IDnsResolver`. It dials the mapped port, names the mapped destination in the CONNECT request, and passes the URL's host to TLS. `HttpProxyTunnel.BuildConnectRequest` (internal) now takes the host and port instead of a `ConnectTarget`, so a mapped port 0 (which `ConnectTarget` refuses) still works. Tests: `ResolveOverridesTests`, `ConnectToMappingsTests`, `TcpConnectorTests.Overrides.cs`.
- Choices taken (rule 1): the parsed tables are handed to `TcpConnector` rather than put behind `IDnsResolver`. `IDnsResolver.ResolveAsync` takes no port, `--resolve` is keyed on host and port, and changing the Abstractions contract is outside this task's `touches`. No ADR was written: every behaviour is measured curl behaviour, not a new decision. `Documentation/Planning/Decisions` is also claimed by BL-164 in Doing. For BL-244: build `ResolveOverrides.Parse(options.ResolveEntries)` and `new ConnectToMappings(options.ConnectToEntries)` and pass them to `TcpConnector`.
- Not measured, so not pinned in tests: the `via` text when the mapped host is an IPv6 literal (it prints the host without brackets), and the exit 7 "over proxy" message when a mapping is in use (it keeps the URL's host).
- Measured on curl 8.21.0 (`/mingw64/bin/curl`), `curl -sS -v --connect-timeout 1 <options> http://a/`, 2026-09-27:
  - `--resolve a:80:127.0.0.1 http://A:80/` -> `Trying 127.0.0.1:80` (host matches without regard to case); `a:81:127.0.0.1` -> not used for port 80.
  - `*:80:127.0.0.2 http://zz/` -> `RESOLVE *:80 using wildcard`, `Trying 127.0.0.2:80`.
  - `a:80:127.0.0.3,127.0.0.4` -> tries .3 then .4; `a:80:127.0.0.3` then `a:80:127.0.0.4` -> `old addresses discarded`, tries .4; then `-a:80` -> entry dropped.
  - Accepted: `a:80:[::1]`, `a:80:::1`, `a:80:[127.0.0.1]`, `a:80:[::1]x` (-> ::1), `a:80:127.0.0.1,`, `a:80:,127.0.0.1`, `a:080:…` (port 80), `a:0:…`, `[::1]:80:127.0.0.1` (`Added ::1:80:127.0.0.1`).
  - Ignored silently (no exit 49): `:80:127.0.0.1`, `-a`, `-a:x`, `+`, `[::1:80:127.0.0.1`.
  - Exit 49 `curl: (49) Could not parse CURLOPT_RESOLVE entry '<entry>'`: `a:80:notanip`, `a:80:127.0.0.1:81`, `a:80:+127.0.0.1`, `a:80:01.2.3.4`, `a:80:1.2.3`, `a:80:1..2.3`, `a:80:1.2.3.1234`, `a:80:1.2.3.256`, `a:80:fe80::1%1`, `a:80:::g`, `a:80:[::1`, `a:80:,`, `a:99999:…`, `a:123456:…`, `a:-1:…`, `a:+80:…`, `a:80x:…`, `a::127.0.0.1`. The first bad entry stops parsing: `a:80:127.0.0.3` then `a:80:127.0.0.4,xx` -> exit 49 naming the second.
  - `--connect-to a:80:127.0.0.1:9` and `A:80:…` -> `Trying 127.0.0.1:9`; `::127.0.0.1:` -> `Trying 127.0.0.1:80`; `a::[::1]:9` -> `Trying [::1]:9`; `a:80:127.0.0.8` and `…:` -> port 80; `…:0080` and `…:0000000000080` -> 80; `…:9x` -> 9; `a:80:[::1]` -> `[::1]:80`; `a:80:[::1]x:9` -> `[::1]:9`; `a:0000000000080:127.0.0.8:9` -> 127.0.0.8:9; `…:65535` accepted.
  - First match wins: `a:81:127.0.0.1:9` then `a:80:127.0.0.6:9` -> .6; `a:80::` then `a:80:127.0.0.7:9` -> resolves `a` (an empty match still stops the search).
  - Never matching, so never parsed: `a:x:127.0.0.1:9`, `a:80`, `[::1:80:127.0.0.1:9`, `b:80:c:x` (for URL `a`); `::1:80:127.0.0.9:9` does not match `http://[::1]/`, `[::1]:80:127.0.0.9:9` does.
  - Exit 49: `a:80:b:x` -> `curl: (49) No valid port number in 'b:x'`; `b:99999`, `127.0.0.8:-1`, `:+0`, `:-0`, `:65536`, `:1234567890` likewise; `a:80:[::1:9` -> `curl: (49) Invalid IPv6 address format in '[::1:9'`.
  - `--connect-to a:80::9 --resolve a:9:127.0.0.5` -> `Host a:9 was resolved`, `Trying 127.0.0.5:9` (`--resolve` is keyed on the mapped host and port).
  - `--connect-to a:80:127.0.0.8:0` -> `curl: (7) Failed to connect to a:80 via 127.0.0.8:0 after 0 ms: Could not connect to server`; `--connect-to a:80::0 --resolve a:0:127.0.0.1` -> `… a:80 via a:0 after 0 ms …`; `--resolve a:80:0.0.0.0` (no mapping) -> `Failed to connect to a:80 after 0 ms: …` (no `via`).
  - `--connect-to a:80:nosuch.invalid:81` -> `curl: (6) Could not resolve host: nosuch.invalid`.
  - Proxy, against a Python socket that prints what it receives: `curl -p -x http://127.0.0.1:38123 --connect-to a:80:b.example:81 http://a/` -> `CONNECT b.example:81 HTTP/1.1
Host: b.example:81
User-Agent: curl/8.21.0
Proxy-Connection: Keep-Alive

`; `curl -p -x http://p.example:38123 --resolve p.example:38123:127.0.0.1 http://a/` -> the proxy is dialled at 127.0.0.1 (`--resolve` applies to the proxy host).
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary -IncludeIntegration` puts every new and changed member at 100% line and branch coverage and complexity 10 or less. One member still fails: `SslStreamTlsProvider.CreateCipherSuitesPolicy()` line 272, the non-Windows branch, which cannot run on Windows. It was already failing, this task does not touch it, and BL-268 owns it (the same position BL-211 recorded). Without `-IncludeIntegration`, the loopback-only lines in `TcpDialer` and `UdpDatagramChannel` also show as uncovered; they too were uncovered before this task.

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. TcpConnector honours --resolve (ResolveOverrides) and --connect-to (ConnectToMappings) with curl 8.21.0's parse rules, exit 49 messages and via text
