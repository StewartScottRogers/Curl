---
id: BL-642
title: Parse --doh-url, --doh-insecure and --doh-cert-status and resolve through DoH
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-641, BL-610]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests, Documentation/Planning/Decisions/ADR-0152-dns-over-https-posts-a-and-aaaa-in-parallel-over-a-minimal-http-1-1-exchange-inside-networking.md]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-642 — Parse --doh-url, --doh-insecure and --doh-cert-status and resolve through DoH

## Goal

`--doh-url <url>` makes every transfer resolve through `DohDnsResolver` (BL-641), `--doh-insecure` skips the DoH server's certificate check, `--doh-cert-status` checks the DoH server's stapled OCSP response exactly as `--cert-status` does for the transfer (BL-610; on every platform, standing rule, root `CLAUDE.md`, "Decisions", 2026-09-28), and `-v` writes curl's DoH lines.

## Context

- Conformance audit 2026-09-28, row 27. Resolver: BL-641; design: BL-639's ADR.
- Parse in `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs`; compose the resolver in `Curl.Console/CurlTransports.cs`. `--resolve` entries still win over DoH if curl's do (measure).

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1 -Tls -k` as the DoH server: `-v --doh-url ... --doh-insecure http://example.test:<P>/` with an A answer pointing at a second recorder, `--doh-url` with `--resolve` for the same host, `--doh-url bogus`, and `--doh-cert-status`; stderr, exit code and both recorders' requests copied into Notes.
- [x] `Curl.Cli.UnitTests` cover parsing and refusals; `Curl.Console.UnitTests` pin each measured case through fake connectors.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- Measured 2026-09-29, curl 8.21.0 Schannel. Two recorders at once: an outer plain `Record-CurlExchange.ps1 -Port 48712` (the transfer's server) whose `-Curl` is `powershell.exe` running an inner `Record-CurlExchange.ps1 -Port 48711 -Tls -Connections 2` (the DoH server), which runs curl; no script change was needed. Every DoH connection was answered `HTTP/1.1 200 OK`, `Content-Type: application/dns-message`, `Content-Length: 46`, and the A answer `00 00 81 80 00 01 00 01 00 00 00 00 07 example 04 test 00 00 01 00 01 C0 0C 00 01 00 01 00 00 00 3C 00 04 7F 00 00 01`.
  - `-sS -v --doh-url https://127.0.0.1:48711/dns-query --doh-insecure http://example.test:48712/`: exit 0, stdout `hi`. DoH recorder got two POSTs (306 bytes): `POST /dns-query HTTP/1.1`, `Host: 127.0.0.1:48711`, `Accept: */*`, `Content-Type: application/dns-message`, `Content-Length: 30`, the 30-byte query with QTYPE `00 01`, then the same with `00 1C`. Web recorder got `GET / HTTP/1.1`, `Host: example.test:48712`, `User-Agent: curl/8.21.0`, `Accept: */*`. stderr: `* Host example.test:48712 was resolved.` / `* IPv6: (none)` / `* IPv4: 127.0.0.1` / `*   Trying 127.0.0.1:48712...` / `* Established connection to example.test (127.0.0.1 port 48712) from ...` then the usual HTTP lines. `-v` writes no DoH-specific line (as ADR-0152 found).
  - with `--resolve example.test:48712:127.0.0.1`: exit 0; stderr starts `* Added example.test:48712:127.0.0.1 to DNS cache` / `* Hostname example.test was found in DNS cache` / the three resolved lines; DoH recorder got 0 bytes, web recorder the GET. `--resolve` wins.
  - `--doh-url bogus`: exit 6 after about 7 s, stderr `* Could not resolve host: example.test` / `* Could not resolve: example.test:48712` / `* closing connection #0` / `curl: (6) Could not resolve host: example.test`; neither recorder got a byte. `--doh-url ftp://127.0.0.1:48711/`: the same four lines, exit 6. `--doh-url 127.0.0.1:48711/dns-query` (no scheme): exit 6, the TLS recorder saw a non-TLS client (scheme guessed `http`).
  - `--doh-cert-status --doh-insecure`: exit 0, both POSTs (306 bytes), the GET: the Schannel build ignores the status, as it ignores `--cert-status` (BL-610).
  - `-k` without `--doh-insecure`: exit 6, `curl: (6) Could not resolve host: example.test`, nothing reached either recorder. `--ssl-no-revoke --cacert <recorder root> ` without `--doh-insecure` (`-TlsRootCertificateFile`): exit 0, resolved (without `--ssl-no-revoke`: exit 6, Schannel's revocation check of the root-less-endpoint chain fails).
  - `-4`: one 153-byte A POST, exit 0; `-6`: one AAAA POST, exit 6 (no AAAA answer).
  - `--doh-url ''` exit 7 against `127.0.0.1:1` (accepted); `--no-doh-insecure`, `--no-doh-cert-status` accepted; `--doh-url` last: `curl: option --doh-url: requires parameter`, exit 2; `--no-doh-url x`: `the given option cannot be reversed with a --no- prefix`, exit 2.
- Decisions (ADR-0152's BL-642 amendment, decided by Claude under Stewart's delegation): the DoH connections take curl's `lib/doh.c` set of transfer TLS options (`--cacert`, `--capath`, `--crlfile`, `--curves`, `--ssl-no-revoke`, `--ssl-revoke-best-effort`, `--ssl-auto-client-cert`) plus the two DoH flags; `--doh-cert-status` reaches the DoH handshakes verbatim and ADR-0191 applies (the Schannel build's silence is not matched, as for `--cert-status`), replacing ADR-0152 point 3's "skipped under `--doh-insecure`"; a scheme-less value gets `http://`; a non-`http(s)` or unparsable value resolves nothing (`UnusableDohUrlResolver`, exit 6); an empty value turns DoH off; `--doh-url` wins over the c-ares options. ADR-0152 was added to `touches` for the amendment; no task in Doing names it.
- Built: `CommandLineOptions.DohUrl`, `DohInsecure`, `DohCertificateStatus` and their rows (per-group, as curl keeps them in `OperationConfig`); `TlsClientOptionsMapping.DohFromCommandLine`; `CurlComposition.CreateDnsResolver(options, timeProvider, tcpDialer)` (DoH first), `CreateDohResolver`, `DohUrlOf`, `CreateDohConnector` (its own `TcpConnector` over the system resolver, no `--resolve`/`--connect-to`/proxy, ALPN `http/1.1`); `UnusableDohUrlResolver`.
- Sensible default: `-4`/`-6` still send both queries (the addresses are filtered, so output matches); sending only the family's query needs `Curl.Networking.UnitLibrary`, which BL-819 in Doing touches, so it is filed as BL-915 instead of widening this task.
- `--ai-help` does not exist yet (BL-911 is in Backlog), so there is nothing to keep right; BL-911 will cover these rows from the option table.
- Tests: `CommandLineDohOptionTests` (12 incl. rows), `CurlCompositionDohTests` (21 incl. rows). `Measure-CodeQuality.ps1`: `Curl.Cli.UnitLibrary` and `Curl.Console` each 100% line, 100% branch, 0 failing members, worst CRAP 10. Full fast run green.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. --doh-url resolves every transfer through DohDnsResolver, --doh-insecure and --doh-cert-status set the DoH handshakes' checks, --resolve still wins, and a bogus or non-HTTP DoH URL fails with exit 6
