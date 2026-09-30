---
id: BL-709
title: Apply --curves and --sigalgs on every platform
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-618, BL-708]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-30
---
# BL-709 — Apply --curves and --sigalgs on every platform

## Goal

`--curves <list>` restricts the key-exchange groups and `--sigalgs <list>` the signature algorithms offered in the handshake, on Windows, Linux and macOS, with curl 8.21.0's syntax (OpenSSL-style colon lists), its failure for an unknown name, and its failure when the server shares nothing.

## Context

- Conformance audit 2026-09-28, row 18; standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28). curl: `--curves` "Set specific curves to use during SSL session establishment according to RFC 8422, 5.1." (https://curl.se/docs/manpage.html, checked 2026-09-28).
- Parsing: BL-618. How each platform carries it (`SslStream` cannot restrict groups per connection; the hand-built client can): BL-617's ADR; the routing row is added to BL-708's function.
- Measure with an OpenSSL build of curl through `Record-CurlExchange.ps1 -Tls -k`: `--curves X25519`, `--curves bogus`, `--sigalgs ECDSA+SHA256`, `--sigalgs bogus`, and a server limited to P-384 with `--curves X25519`; stderr and exit code; also the ClientHello groups and signature algorithms captured with `-NoServer`.

## Acceptance criteria

- [x] Measured first as above; copied into Notes.
- [x] Tests pin the ClientHello `supported_groups`/`key_share` and `signature_algorithms` for each option value, and the measured failures, on every platform (no `OSCondition` refusal).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- Measured 2026-09-30. ClientHellos: `Record-CurlExchange.ps1` in plain TCP mode (no `-Tls`, so the ClientHello lands in `request.bin`) with `-Curl wsl.exe -ListenAddress 172.26.96.1`, Ubuntu curl 8.18.0 / OpenSSL 3.5.5. Failures: `openssl s_server` in WSL (`-groups P-384` for the no-shared-group case), run from the OpenSSL build and from curl.se's 8.18.0 / LibreSSL 4.2.1 (through WSL interop). Every table is in ADR-0284's Context; the key rows:
  - Default: groups `11ec 001d 0017 001e 0018 0019 0100 0101`, key shares `11ec 001d`. `--curves X25519`: `001d` / `001d`. `P-384:X25519`: `0018 001d` / `0018`. `X25519MLKEM768`: `11ec` / `11ec`. `--sigalgs ECDSA+SHA256`: `0403`; `rsa_pss_rsae_sha256:ECDSA+SHA256`: `0804 0403`.
  - `--curves bogus`: exit 59 `curl: (59) failed setting curves list: 'bogus'` (both builds). `--sigalgs bogus`: exit 59 `curl: (59) failed setting signature algorithms: 'bogus'` (OpenSSL; curl.se ignores it).
  - Server limited to P-384, `--curves X25519`: OpenSSL `curl: (35) TLS connect error: error:0A000410:SSL routines::ssl/tls alert handshake failure`; curl.se `curl: (35) TLS connect error: error:14004410:SSL routines:CONNECT_CR_SRVR_HELLO:sslv3 alert handshake failure`. RSA-only server with `--sigalgs ECDSA+SHA256`: the OpenSSL line above.
  - `--curves ?bogus` / `-X25519`: exit 35 `...error:0A000127:SSL routines::no suitable groups`; `--sigalgs RSA+SHA1`: exit 35 `...error:0A000076:SSL routines::no suitable signature algorithm`. Order: curves 59, sigalgs 59, no groups 35.
- Decision: ADR-0284 (0283 was already taken by lane 2's BL-991 on its branch). One OpenSSL 3.5 syntax everywhere (`OpenSslGroupList`, `OpenSslSignatureAlgorithmList`), applied by `CurvesAndSignatureAlgorithms.Apply` to the platform profile's three lists; groups and schemes the client cannot run are dropped as unrunnable cipher suites are; the routing row is `Curves or SignatureAlgorithms not null` in `TlsClientRouting`. On Windows a handshake failure under `--sigalgs` prints OpenSSL's text, and a `handshake_failure` alert under `--curves` alone prints curl.se's; any other `--curves` failure keeps Schannel's, the only other text not having been measured from curl.se.
- Defaults taken: the exit 35 "no suitable" failures are reported before `--cert` is loaded (OpenSSL reports them at connect, after loading it; only a bad `--cert` together with an empty list tells the difference). `DEFAULT` expands to the Schannel profile on Windows. The `--curves`/`--sigalgs` proxy forms are not parsed yet (BL-618 carried the origin forms only), so the HTTPS proxy's handshake is unchanged.
- `touches` gained `Documentation/Planning/Decisions` for ADR-0284 and its index row; no task in Doing names it (BL-991 touches only the SSH projects).
- `--ai-help` needs no change: the option set and its text are unchanged; only the behaviour behind `--curves`/`--sigalgs` is new.
- Filed: BL-1049 (ML-KEM hybrid, pure ML-KEM and brainpool TLS 1.3 groups), BL-1047 (ML-DSA, ed448, brainpool TLS 1.3 ECDSA schemes), BL-1048 (`ec_point_formats`, `padding` and brainpool groups beside TLS 1.3 in OpenSSL's hello).
- Quality: `Curl.Networking.UnitLibrary` 100% line, 100% branch, 869 members, 0 failing (worst CRAP 10); `Curl.Console` 100%/100%, 0 failing.

## Log

- 2026-09-28: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. --curves and --sigalgs now shape the hand-built client's ClientHello on every platform with OpenSSL 3.5's syntax, curl's exit 59 and 35 texts and the applying build's handshake-failure line
