---
id: BL-5281
title: Log in with SASL GSSAPI over SMTP, IMAP and POP3
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Authentication.UnitLibrary/SaslAuthenticator.cs, Curl.Authentication.UnitTests/SaslGssapiLoginTests.cs]
requirement: none
created: 2026-11-16
completed:
---
# BL-5281 — Log in with SASL GSSAPI over SMTP, IMAP and POP3

## Goal

With `--login-options AUTH=GSSAPI` and `-u user@REALM:password`, the port completes a SASL `GSSAPI` (Kerberos) login over SMTP, IMAP and POP3 on Windows, as upstream curl 8.21.0 does through SSPI.

## Context

Filed from Surl's Phase 7 measurement of the port (Stewart approved filing on the port's board, 2026-11-16): Surl `Documentation/Conformance/CurlPort/Port-Board-Handoff.md`, entry 3 (github.com/StewartScottRogers/Surl). The port was measured at commit `b42c319ba940f66452b2b6f3e9910d46825b08fe` (port `curl.exe` SHA-256 `F28D1417807AA862BA2A6D5B0BF0CF1200673BE45B959B1A78E1E4B476EF8FA7`), on Windows x64, 2026-11-05, beside upstream curl 8.21.0, the oracle: where the two differ against the same Surl server, the port is the one in the wrong.

- **Finding:** Surl `Documentation/Conformance/CurlPort/Http.md`, finding 3.
- **Surl tests (6):** `CurlPortBesideUpstreamCurlOverHttpTests.GssapiOverSmtp_Account_StoresTheMessageInTheAccountsInbox`, `GssapiOverImap_Account_FetchesTheDeliveredMessage` and `GssapiOverPop3_Account_RetrievesTheDeliveredMessage`, each with and without `--sasl-ir`; Surl commit `489f8d6f9b80614937f188b1a41896b8c5ed0e5c`.
- **Command lines**, against `surl --auth gssapi --keytab <keytab> --user-file <tester@SURL.TEST>` and a test KDC on `127.0.0.1:88`, with `--resolve mail.surl.test:<port>:127.0.0.1`:
  - `curl -sS -u tester@SURL.TEST:surl-test-password --login-options AUTH=GSSAPI --mail-from a@x --mail-rcpt "tester@SURL.TEST"@example.com -T mail.txt smtp://mail.surl.test:<port>/c`
  - `curl -sS -u tester@SURL.TEST:surl-test-password --login-options AUTH=GSSAPI imap://mail.surl.test:<port>/INBOX;UID=1`
  - `curl -sS -u tester@SURL.TEST:surl-test-password --login-options AUTH=GSSAPI pop3://mail.surl.test:<port>/1`
- **Upstream curl 8.21.0 (correct):** reference build, SHA-256 `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`: exit 0 in all six; SMTP stores the message, IMAP and POP3 write it (first line `Return-Path: <a@x>`).
- **Port (defect):** exit 67, `curl: (67) Login denied`, in all six; nothing stored or written.
- **What decides:** RFC 4752 (`GSSAPI` SASL mechanism), carried by RFC 4954 (SMTP), RFC 9051 section 6.2.2 (IMAP) and RFC 5034 (POP3); curl 8.21.0's `--login-options` (https://curl.se/docs/manpage.html) selects it.
- **Where to start:** `Curl.Authentication.UnitLibrary/SaslAuthenticator.cs` and the GSSAPI step through the Negotiate security context; the per-protocol login code in `Curl.Original.Protocol.Smtp/Imap/Pop3.UnitLibrary` (earlier work: BL-538).

## Acceptance criteria

- [ ] The port completes a SASL `GSSAPI` login over SMTP, IMAP and POP3, with and without `--sasl-ir`, shown by port tests of each exchange.
- [ ] `--login-options AUTH=GSSAPI` with a valid Kerberos principal no longer exits 67 (`CURLE_LOGIN_DENIED`) where the server accepts the ticket.

## Notes

## Log

- 2026-11-16: Created.
