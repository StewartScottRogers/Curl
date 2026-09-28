---
id: BL-502
title: Negotiate TLS 1.0 and 1.1 minimums and the --tls-max ceiling in SslStreamTlsProvider
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-501]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests, Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-502 — Negotiate TLS 1.0 and 1.1 minimums and the --tls-max ceiling in SslStreamTlsProvider

## Goal

The TLS provider offers exactly the protocol versions between the minimum (`-1`, `--tlsv1.0` … `--tlsv1.3`) and the `--tls-max` ceiling, for the origin and (with `--proxy-tlsv1`) the HTTPS proxy, and a version range the server or the operating system cannot meet fails with the exit code and message the platform's curl 8.21.0 build gives.

## Context

- Conformance audit 2026-09-28, row 5 (Blocker). Parsing is BL-501.
- Code: `Curl.Networking.UnitLibrary/SslStreamTlsProvider.cs`, `TlsClientOptions.cs`, `TlsMinimumVersion.cs`, `TlsFailureMessages.cs`; mapping in `Curl.Console/TlsClientOptionsMapping.cs`. The HTTPS proxy's options are separate (ADR-0095).
- ADR-0009: match the platform's build (Schannel on Windows, OpenSSL elsewhere). Windows 11 disables TLS 1.0/1.1 in the OS; Schannel's answer to `--tls-max 1.1` against a TLS 1.2-only server must be measured, as must a min above the max (`--tlsv1.3 --tls-max 1.2`).
- This task covers what `SslStream` can negotiate. Where the operating system's TLS stack refuses TLS 1.0/1.1 but an official curl build connects (curl.se's LibreSSL Windows build), BL-714 closes the gap through the hand-built TLS client (standing rule, root `CLAUDE.md`, "Decisions", 2026-09-28); pin today's `SslStream` answer here and name BL-714 in the XML docs.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1 -Tls -k`: `--tls-max 1.2`, `--tls-max 1.1`, `--tlsv1.3 --tls-max 1.2`, `--tlsv1.0`, each with `-v`; stdout, stderr and exit code copied into Notes.
- [x] Tests on the options mapping show the `SslProtocols` set offered for each min/max pair, with the obsolete members confined to one documented, suppressed place.
- [x] Each measured failure is pinned as its exit code and message, the Windows answer under `[OSCondition(OperatingSystems.Windows)]` and the OpenSSL-build answer in its own excluded-Windows test.
- [x] `--proxy-tlsv1` reaches the proxy's TLS options only, with a test.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Networking.UnitLibrary` and `Curl.Console`.

## Notes

Measured 2026-09-28 with `Record-CurlExchange.ps1 -Tls -Port 18502 -CurlArgs -k,-v,-s,<args>,https://127.0.0.1:18502/`
(curl 8.21.0, Schannel, x86_64-w64-mingw32; the recorder's server is TLS 1.2 only and answers an empty
`200` with `Content-Length: 0`). stdout was empty in every case.

`--tls-max 1.2` — exit 0; `--tlsv1.0` — exit 0, the same stderr (bar the local port):

```
*   Trying 127.0.0.1:18502...
* schannel: disabled automatic use of client certificate
* schannel: using IP address, SNI is not supported by OS.
* ALPN: curl offers http/1.1
* ALPN: server did not agree on a protocol. Uses default.
* Established connection to 127.0.0.1 (127.0.0.1 port 18502) from 127.0.0.1 port 57222 
* using HTTP/1.x
> GET / HTTP/1.1
> Host: 127.0.0.1:18502
> User-Agent: curl/8.21.0
> Accept: */*
> 
* Request completely sent off
< HTTP/1.1 200 OK
< Content-Length: 0
< 
* Connection #0 to host 127.0.0.1:18502 left intact
```

`--tls-max 1.1` — exit 35:

```
*   Trying 127.0.0.1:18502...
* schannel: disabled automatic use of client certificate
* schannel: using IP address, SNI is not supported by OS.
* ALPN: curl offers http/1.1
* schannel: failed to receive handshake, SSL/TLS connection failed
* closing connection #0
```

(with `-sS` in place of `-v -s`: `curl: (35) schannel: failed to receive handshake, SSL/TLS connection failed`;
the same for `--tls-max 1.0`, `--tlsv1.1 --tls-max 1.1`, `--tlsv1.0 --tls-max 1.0` and `--tlsv1.0 --tls-max 1.1`).

`--tlsv1.3 --tls-max 1.2` — exit 2, refused while parsing, before any connection:

```
curl: option --tls-max: is badly used here
curl: try 'curl --help' or 'curl --manual' for more information
```

Without `-s` (or with `-sS`) a first line comes before those two: `curl: --tls-max set lower than minimum accepted version`.
In the other order, `--tls-max 1.2 --tlsv1.3`, it is `curl: Minimum TLS version set higher than max` and
`curl: option --tlsv1.3: is badly used here`. `--tls-max default` after any minimum (`-1`, `--tlsv1`,
`--tlsv1.0`, `--tlsv1.2`, `--tlsv1.3`) is refused the same way; before one it is accepted.
`--tlsv1.3 --tls-max 1.3`, `-1 --tls-max 1.0`, `--proxy-tlsv1 --tls-max default` are accepted.

OpenSSL build (Ubuntu curl 8.18.0, OpenSSL 3.5.5, under WSL, against `https://example.com/`, `-sS`):
`--tls-max 1.1`, `--tls-max 1.0`, `--tlsv1.1 --tls-max 1.1`, `--tlsv1.0 --tls-max 1.0` are exit 35,
`curl: (35) TLS connect error: error:0A0000BF:SSL routines::no protocols available`; `--tls-max 1.2` and
`--tlsv1.0` exit 0; `--tlsv1.3 --tls-max 1.2` is the same exit-2 refusal as Schannel's.

HTTPS proxy (`-sS --proxy-insecure -x https://127.0.0.1:18502 <args> http://example.invalid/`, a TLS 1.2-only
proxy): `--tls-max 1.1`, `--tlsv1.3`, `--proxy-tlsv1` and `--proxy-tlsv1 --tls-max 1.1` all exit 0, so neither the
target's minimum nor `--tls-max` reaches the proxy; only `--proxy-tlsv1` does.

`SslStream` probes (a C# file-based app): on Windows 11 a client offering TLS 1.0 alone or TLS 1.1 alone fails with
`SEC_E_UNSUPPORTED_FUNCTION` before sending a byte, one offering both gets the server's alert as
`SEC_E_ILLEGAL_MESSAGE`; on Ubuntu (a self-contained linux-x64 publish run under WSL) each throws OpenSSL's
`error:0A0000BF:SSL routines::no protocols available`, curl's own text.

Decisions (ADR-0138, decided under Stewart's delegation):
- `TlsMinimumVersion` renamed `TlsVersion` (both ends of the range; `SystemDefault` means unset), with
  `TlsClientOptions.MaximumVersion` added last so positional callers are unchanged.
- `TlsVersionRange.ToSslProtocols` is the one suppressed `SYSLIB0039` place in `Curl.Networking.UnitLibrary`; no
  minimum with a ceiling below 1.3 starts at TLS 1.0 (curl's Schannel default); no minimum and no ceiling stays
  `SslProtocols.None`.
- The Schannel build reports any security status as `failed to receive handshake` when the ceiling is 1.0 or 1.1,
  which is what curl printed in all five measured ranges.
- The empty-range refusals are the parser's: this needed `Curl.Cli.UnitLibrary` and `Curl.Cli.UnitTests`
  (`CommandLineOption.FlagThatCanRefuse`, two `CommandLineRefusal`s), added to `touches` because real curl refuses
  at parse time, before any TLS; no task in Doing touches them (BL-674 touches `Curl.Cryptography.*` only).
  `Documentation/Planning/Decisions` was added for the ADR.
- The OpenSSL handshake tests are `[OSCondition(OperatingSystems.Linux)]` rather than "not Windows", because macOS's
  `SslStream` is not OpenSSL; the OpenSSL text is also pinned platform-neutrally in `TlsFailureMessagesTests`.

End to end, the built `curl.exe` through the same recorder matches the reference build byte for byte for
`--tls-max 1.2`, `1.1`, `1.0`, `--tlsv1.1 --tls-max 1.1`, `--tlsv1.0`, `--tlsv1.0 --tls-max 1.1`, the three proxy
cases, and the refusals `--tlsv1.3 --tls-max 1.2`, `-s --tls-max 1.2 --tlsv1.3`, `-1 --tls-max default`.

Quality: `Measure-CodeQuality.ps1` — `Curl.Networking.UnitLibrary` 100/100 (365 members, 0 failing),
`Curl.Console` 100/100 (502, 0), `Curl.Cli.UnitLibrary` 100/100 (663, 0).

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. The handshake offers every TLS version from -1/--tlsv1.x to --tls-max, an empty range is refused at parse time, and each measured failure prints the platform build's exit 35 line
