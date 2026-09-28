---
id: BL-548
title: Authenticate a POP3 session with USER and PASS, APOP or SASL AUTH
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-547, BL-536]
touches: [Curl.Protocol.Pop3.UnitLibrary, Curl.Protocol.Pop3.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-548 — Authenticate a POP3 session with USER and PASS, APOP or SASL AUTH

## Goal

The POP3 handler logs in as curl 8.21.0 does: SASL `AUTH` through the injected authenticator when `CAPA` offers SASL, else `APOP` when the greeting carries a timestamp, else `USER`/`PASS`, with `--login-options AUTH=+APOP`/`AUTH=<mech>` steering the choice, and a refusal mapped to exit 67 and curl's message.

## Context

- Conformance audit 2026-09-28, rows 23 and 34. SASL: BL-533's ADR, BL-534, BL-536. APOP is MD5 of timestamp and password (RFC 1939; `System.Security.Cryptography.MD5`).
- Measure with `Record-CurlExchange.ps1 -Pop3`: `-u u:p` with SASL offered, with only `USER` offered, with an APOP timestamp and no SASL, with `--login-options AUTH=+APOP`, and with `PASS` answered `-ERR`.

## Acceptance criteria

- [x] Measured first as above; request lines, stderr and exit code copied into Notes.
- [x] `Curl.Protocol.Pop3.UnitTests` pin the client bytes (the APOP digest byte for byte) and outcome for each case.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Pop3.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

### Measured (curl 8.21.0 Schannel, `Record-CurlExchange.ps1 -Pop3`, 2026-09-28)

`-sS -u u:p pop3://127.0.0.1:<port>/` unless stated. Default greeting
`+OK POP3 ready <1896.697170952@localhost>`, default CAPA `USER`, `SASL PLAIN LOGIN`, `STLS`,
`TOP`, `UIDL`. Every success ends `LIST`, `QUIT`, exit 0, empty stderr.

| Case | Client lines after `CAPA` | Exit, stderr |
| --- | --- | --- |
| sasl (defaults) | `AUTH PLAIN`, (`+ `) `AHUAcA==` | 0 |
| saslir `--sasl-ir` | `AUTH PLAIN AHUAcA==` | 0 |
| login `--login-options AUTH=LOGIN` | `AUTH LOGIN`, `dQ==`, `cA==` | 0 |
| authstar `AUTH=*` | as sasl | 0 |
| useronly: greeting `+OK POP3 ready`, CAPA `USER` | `USER u`, `PASS p` | 0 |
| userlowonly: CAPA `user` | `USER u`, `PASS p` | 0 |
| userlower: CAPA `user`, `sasl plain` | `AUTH PLAIN`, `AHUAcA==` | 0 |
| apop: CAPA `USER APOP` | `APOP u d727ab40e6dcedbb6cf2f6735fe51cc8` | 0 |
| tsuseronly: timestamp, CAPA `USER` | `APOP u d727...cc8` | 0 |
| capaerr-ts: CAPA `-ERR no` | `APOP u d727...cc8` | 0 |
| capaerr: no timestamp, CAPA `-ERR no` | `USER u`, `PASS p` | 0 |
| saslunusable: `SASL SCRAM-SHA-256` | `APOP u d727...cc8` | 0 |
| capasaslnomech: `SASL` alone, `USER`, no timestamp | `USER u`, `PASS p` | 0 |
| forceapop `AUTH=+APOP` (SASL offered) | `APOP u d727...cc8` | 0 |
| apoplower `auth=+apop` | `APOP ...` | 0 |
| urlapop `pop3://u:p;AUTH=+APOP@...` | `APOP ...` | 0 |
| emptyuser `-u :p` | `USER ` (nothing after), `PASS p` | 0 |
| nocreds (no `-u`) | `LIST` straight away | 0 |
| bearernouser `--oauth2-bearer tok --sasl-ir`, `SASL XOAUTH2` | `AUTH XOAUTH2 dXNlcj0BYXV0aD1CZWFyZXIgdG9rAQE=` | 0 |
| bearerfallback `--oauth2-bearer tok`, CAPA `USER` only | nothing | 67 `Login denied` |
| passerr `PASS=-ERR denied` | `USER u`, `PASS p` | 67 `curl: (67) Access denied. -` |
| usererr `USER=-ERR who` | `USER u` | 67 `Access denied. -` |
| userplus / passplus `+junk` | `USER u` (, `PASS p`) | 67 `Access denied. *` |
| apoperr `APOP=-ERR bad` | `APOP ...` | 67 `Authentication failed: 45` |
| apopplus `APOP=+junk` | `APOP ...` | 67 `Authentication failed: 42` |
| autherr `AUTH=-ERR bad` | `AUTH PLAIN` | 67 `Login denied` |
| authokbare `AUTH=+OK`, `AUTH=LOGIN` | `AUTH LOGIN` | 67 `Login denied` |
| authbad64 `AUTH=+ !!!`, `AUTH=LOGIN` | `AUTH LOGIN`, `dQ==` (then `-ERR`) | 67 `Login denied` |
| plainextra `--sasl-ir`, `AUTH=+ YWJj` | `AUTH PLAIN AHUAcA==`, nothing more | 67 `Login denied` |
| plainjunk `--sasl-ir`, `+junk` then `+OK yes` | `AUTH PLAIN AHUAcA==` | 67 `Login denied` |
| crammissing `AUTH=CRAM-MD5` | nothing | 67 `Login denied` |
| forceapop-nots `AUTH=+APOP`, no timestamp | nothing | 67 `Login denied` |
| nothing: CAPA `TOP` only, no timestamp | nothing | 67 `Login denied` |
| forceuser `AUTH=USER`, othopt `FOO=bar` | nothing, not even `CAPA` | 3 `URL using bad/illegal format or missing URL` |
| irlen177 `--sasl-ir -u u:<177 p>` | `AUTH PLAIN <240 base64>` on one line | 0 |
| irlen180 `--sasl-ir -u u:<180 p>` | `AUTH PLAIN`, then the 244 base64 | 0 |

No login failure sends `QUIT`.

### Decisions

Recorded in ADR-0134 (decided by Claude under Stewart's delegation): a non-base64 challenge
reaches the exchange empty; `+OK` succeeds once the initial response is sent (curl also
refuses one between LOGIN's user name and password - the contract cannot tell); the last
`AUTH=` counts; credentials go out as Latin-1. `Documentation/Planning/Decisions` was added
to `touches` for the ADR and its README row; no task in `Doing` names it.

### Delivered

`Pop3Login` (the choice and the three exchanges), `Pop3LoginOptions`/`Pop3LoginMethod`,
`Pop3ApopDigest`, `Pop3Capabilities.AdvertisesUser`/`SaslMechanisms`, and an optional
`ISaslAuthenticator` on `Pop3ProtocolHandler`. `Curl.Console` does not register the POP3
handler yet; whoever does passes `CurlComposition.CreateSaslAuthenticator()`.
`Pop3ProtocolHandlerLoginTests` pins every case above; POP3 tests 152, all green.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. POP3 logs in with SASL AUTH, APOP or USER/PASS as curl 8.21.0 does, steered by AUTH=+APOP/AUTH=<mech>, refusals exit 67 with curl's text
