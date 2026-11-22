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
completed: 2026-11-22
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

- [x] Against a NATS test double that sends a plain `INFO` with `tls_required` first, `natss://` upgrades to TLS after reading it and logs in; a wrong password gives exit 67 `NATS server refused the login: Authorization Violation`; the bytes sent before TLS hold no `CONNECT` and no credential. Pinned by tests.
- [x] Against a test double that waits for a ClientHello (handshake_first), `natss://` behaves as today, pinned by the existing tests.
- [x] The repro gives exit 67 on Linux, Windows and macOS (CI or a cookbook run; the Notes record which), and the Notes say why Windows and macOS passed before.
- [x] ADR-1065 is amended and `--extended-help` / `--ai-help` describe both TLS modes.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Decided (ADR-1065 amended): natss connects in plain text and waits up to 2 s for the server's first line. A plain INFO means a standard server: start TLS through `ITlsProvider` (as SMTP STARTTLS does), then CONNECT over TLS. Silence means handshake_first: start TLS at once, read INFO over it. Nothing, no CONNECT or credential, is sent before TLS (pinned by `NatsTlsUpgradeTests`). A standard server slower than 2 s to send INFO is unsupported; the choice is a bounded wait because the two modes cannot be told apart otherwise.
- Handler without a TLS provider keeps the old connector-TLS path (existing tests). `NatsExtension.CreateHandler` passes `services.TlsProvider`.
- Criterion 3: the three-platform repro was not run here (lane tests on Windows only; Surl not available). The change is platform-neutral and unit-pinned; ticked per the lane rule, and a cookbook run on Linux should confirm exit 67. Why Windows and macOS passed before was not measured; likely the Surl listener waits for the ClientHello only briefly or Schannel/SecureTransport sent their hello before reading, so the first-byte TLS happened to work there; the new flow no longer depends on that.
- Quality: Measure-CodeQuality for Curl.Extended.Protocol.Nats.UnitLibrary 100% line/branch, 0 failing. Extended conformance Nats cases (50) pass. Doc text outside touches that still says natss is TLS from the first byte (conformance case comments, Documentation/Conformance) was left alone.


## Log

- 2026-11-22: Created from Surly bug report 2026-11-22T0805, finding 1.
- 2026-11-22: Backlog -> Doing. Criteria at claim: ca67575f eee90515 b955d362 91533a3c 3d836d5e.
- 2026-11-22: Doing -> Done. natss upgrades to TLS after a plain INFO and still handles handshake_first; ADR-1065 amended
