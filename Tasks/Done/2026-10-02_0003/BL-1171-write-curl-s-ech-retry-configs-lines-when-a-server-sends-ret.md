---
id: BL-1171
title: Write curl's ECH: retry_configs lines when a server sends retry_configs for --ech
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1107]
touches: [Curl.Tls.UnitLibrary, Curl.Tls.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Output.UnitLibrary, Curl.Output.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1171 — Write curl's ECH: retry_configs lines when a server sends retry_configs for --ech

## Goal

When a server sends `retry_configs`, `-v` writes curl 8.21.0's `ECH: retry_configs <base64>` and `ECH: retry_configs for <inner> from <outer>, <reason> <rv>` lines. This covers a rejected ECH offer before exit 101, and GREASE answered by an ECH server.

## Context

- ADR-0359 (BL-1107). Today `HandBuiltTlsProvider.ReportEchRejection` always writes `ECH: no retry_configs (rv = 1)`, which is right only for a server that sends none.
- Measured with GREASE against `openssl s_server -ech_key`:
  - `ECH: result: status is sent GREASE, got retry-configs, inner is NULL, outer is NULL`
  - `ECH: retry_configs <the server's list in base64>`
  - `ECH: retry_configs for NULL from NULL, 0 3`
- `Tls13ClientHandshake` checks and drops the retry configs sent in answer to GREASE. A rejection's configs are on `EncryptedClientHelloRetryConfigs`, but they do not reach `TlsHandshakeFailure`.
- Measure the rejected case with a server whose ECH key differs from the `ecl:` list. Use the Docker recipe in BL-1107's Notes.

## Acceptance criteria

- [x] The rejected case's lines and exit 101 are measured and recorded in Notes.
- [x] `Curl.Networking.UnitTests` pin both cases' lines.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and the touched libraries keep 100% line and branch coverage.

## Notes

### The rejected case, measured 2026-10-02

`curl-ech:8.21.0` (BL-1107's image: curl 8.21.0, OpenSSL 4.0.0), `openssl s_server -www -ech_key a.pem`
(`public_name example.com`), the client given `ecl:` of a second key `b.pem` (`public_name other.example`),
`curl -sS -v -k <args> https://localhost:9443/`. With `--ech ecl:<b>`, `hard`+`ecl:` and `true`+`ecl:`, each exit 101:

```
* ECH: ECHConfig from command line
* TLSv1.3 (OUT), TLS alert, ECH required (633):
* ECH: retry_configs <the server's list a.pem in base64, its 16-bit length included>
* ECH: retry_configs for localhost from other.example, 424 -106
* ECH required: error:0A0001A8:SSL routines::ech required
curl: (101) ECH required: error:0A0001A8:SSL routines::ech required
```

With `hard`+`ecl:`+`pn:x.example` the `ECH: inner: 'localhost', outer: 'x.example'` line follows the
command-line line, and the second retry line reads `for localhost from x.example, 424 -106`: the outer
name is the public name actually sent (the `pn:` override, else the offered config's). `424` is
`SSL_R_ECH_REQUIRED`, `-106` OpenSSL's failed-ECH status; GREASE's `0 3` is no reason and
`SSL_ECH_STATUS_GREASE_ECH`.

### What changed

- Ran directly, not through the `/feature` subagents: the plan was fixed by the task's Context and the measurement.
- `Curl.Tls`: `Tls13ClientHandshake` keeps `retry_configs` sent in answer to GREASE too, and `TlsHandshakeFailure.EchRetryConfigs` carries them to the caller of a failed handshake.
- `Curl.Networking`: `EchRetryConfigsText` writes both cases' lines; `HandBuiltTlsProvider` writes `Rejected`'s before exit 101 (replacing the fixed `no retry_configs` line, still written when none came), and on a completed GREASE handshake sets `EchResultText`'s new `got retry-configs` text and `Grease`'s lines.
- Touches widened to `Curl.Protocol.Abstractions.UnitLibrary`/`.UnitTests` and `Curl.Output.UnitLibrary`/`.UnitTests` (no task in Doing on `origin/work/dark-factory` names them): curl writes the GREASE lines straight after `ECH: result:`, which `OpenSslHandshakeText` writes from the handshake event, so they ride on a new `TlsHandshakeEvent.EchRetryConfigLines`, written after the result line. A provider info line would land after the certificate lines instead.
- Tests: `EchRetryConfigsTextTests`, `EchResultTextTests.Of_WithGreaseAnsweredWithRetryConfigs_...`, and end to end against the in-memory `Fakes/Tls13Server/Tls13TestServer` (new `EchRetryConfigs`): `HandBuiltTlsProviderTests.AuthenticateAsClientAsync_WhenTheServerRejectsTheEchOfferWithRetryConfigs_WritesThemAndFailsWithExit101` and `..._WithEchGreaseAnsweredWithRetryConfigs_ReportsThemAfterTheResult`; `VerboseTransferEventWriterTests.ReportTlsHandshake_OpenSslWithEchRetryConfigLines_WritesThemAfterTheResultLine`.
- `Measure-CodeQuality.ps1`: Networking and Tls 0 failing members.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. -v writes curl's ECH: retry_configs lines for a rejected --ech offer (exit 101) and for GREASE answered with retry_configs
