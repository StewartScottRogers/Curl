---
id: BL-5750
title: Support the standard NATS TLS upgrade (plain INFO, then TLS) for natss://, not only handshake_first
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Nats.UnitLibrary, Documentation/Planning/Decisions]
requirement: none
created: 2026-11-22
completed:
---
# BL-5750 — Support the standard NATS TLS upgrade (plain INFO, then TLS) for natss://, not only handshake_first

## Goal

`natss://` works against a standard NATS server, which sends its `INFO` line in plain text and upgrades to TLS after the client reads it, as well as against one set to `handshake_first`; the same command gives the same result on Windows, Linux and macOS.

## Context

From Surly cookbook bug report `2026-11-22T0805-Curl-bug-report.md` (CI on ubuntu-latest, Ubuntu 26.04, OpenSSL), finding 1 (Medium). Build tested: Curl `c84035f797`; server Surl `5da5b3eaf`.

**Repro (Linux):** `surl --cert server.pem --key server.key --user alice:s3cret natss://127.0.0.1:4443/`, then `curl --cacert server.pem -u alice:wrong -d hi natss://127.0.0.1:4443/orders`: exit 35 `TLS connect error: ... error:0A00010B:SSL routines::wrong version number`, before any login. Expected, as on Windows and macOS: exit 67 `NATS server refused the login: Authorization Violation`. "Wrong version number" is OpenSSL reading the server's plain `INFO` line as a TLS record.

**Cause by design:** ADR-1065 made `natss://` "TLS from the first byte ... the server must run with `tls { handshake_first: true }`", because "`IConnection` has no way to start TLS over an open connection". That premise looks out of date: curl's own SMTP, IMAP and POP3 STARTTLS and FTP `AUTH TLS` upgrade an open connection in this codebase, so the same mechanism (or the contract they use) can serve NATS. The 2026-11-18 fitness review listed "natss requires handshake_first (ADR-1065), so a default NATS TLS server (INFO first) is not supported" as a known deviation; the complete-reimplementation rule (root `CLAUDE.md`, Decisions) says build it. Why Windows and macOS pass against the same Surl listener is part of the task: find out (Surl may wait for a ClientHello before sending INFO, which Schannel and SecureTransport win and OpenSSL loses, or the client's first write differs), so the fix does not rest on a race.

**Do:** read the first bytes the server sends; if they are a plain `INFO {...}` line (and it says `tls_required` or `tls_available`), send nothing in clear, upgrade the connection to TLS the way STARTTLS does, and go on with CONNECT over TLS; if the server waits for a ClientHello (handshake_first), keep today's behaviour. NATS's spec: docs.nats.io, "Client protocol" and "TLS first". Amend ADR-1065 (decided by Claude under Stewart's delegation) and update `--extended-help` and `--ai-help` for natss. No credential ever goes out before TLS.

## Acceptance criteria

- [ ] Against a NATS test double that sends a plain `INFO` with `tls_required` first, `natss://` upgrades to TLS after reading it and logs in; a wrong password gives exit 67 `NATS server refused the login: Authorization Violation`; the bytes sent before TLS hold no `CONNECT` and no credential. Pinned by tests.
- [ ] Against a test double that waits for a ClientHello (handshake_first), `natss://` behaves as today, pinned by the existing tests.
- [ ] The repro gives exit 67 on Linux, Windows and macOS (CI or a cookbook run; the Notes record which), and the Notes say why Windows and macOS passed before.
- [ ] ADR-1065 is amended and `--extended-help` / `--ai-help` describe both TLS modes.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-22: Created from Surly bug report 2026-11-22T0805, finding 1.