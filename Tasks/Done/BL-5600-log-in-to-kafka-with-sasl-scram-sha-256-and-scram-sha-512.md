---
id: BL-5600
title: Log in to Kafka with SASL SCRAM-SHA-256 and SCRAM-SHA-512
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Kafka.UnitLibrary, Curl.Extended.Protocol.Kafka.UnitTests, Curl.Conformance.Extended.UnitTests/Kafka/KafkaSaslTests.cs]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-5600 — Log in to Kafka with SASL SCRAM-SHA-256 and SCRAM-SHA-512

## Goal

The `kafka://` and `kafkas://` client logs in with SCRAM-SHA-256 or SCRAM-SHA-512 when the broker offers it and `-u` is given.

## Context

From Surly cookbook bug report `2026-11-19T1942-Curl-bug-report.md` (batch 1 of the 1,000-recipe plan), finding #5. Build tested by the reports: the Curl port at `b9ca725077` (`curl`, `curl-mcp 0.1.0`, `Curl.Api 0.2.0`, `Libcurl 0.2.0`), Windows 11 (Linux and macOS through the cookbook's CI where stated); server Surl `763754785`. Check that it still reproduces on the current branch first.

The cookbook asked for SCRAM logins for mail and Kafka. Upstream curl has no SCRAM SASL mechanism for mail (its SASL list is LOGIN, PLAIN, CRAM-MD5, DIGEST-MD5, GSSAPI, EXTERNAL, NTLM, XOAUTH2 and OAUTHBEARER), so mail keeps curl's behaviour and gets no task. Kafka is an extension and SCRAM-SHA-256/512 is a standard Kafka SASL mechanism (KIP-84, RFC 5802 and RFC 7677): build it by hand with the BCL's `Rfc2898DeriveBytes` and `HMACSHA256`/`HMACSHA512`. Choose how a user picks the mechanism (offered-order preference, or `--login-options AUTH=SCRAM-SHA-512`) and record it in an ADR. The password never appears in `-v` or `--log-file`.

## Acceptance criteria

- [x] The SCRAM client reproduces RFC 7677's SCRAM-SHA-256 test vector exactly, pinned by a unit test, and a SCRAM-SHA-512 vector likewise.
- [x] Against a Kafka broker (or a test double speaking the SaslHandshake and SaslAuthenticate exchange) configured for SCRAM-SHA-256 and for SCRAM-SHA-512, `curl -u user:pass kafka://...` produces and consumes a message; a wrong password fails with the exit code the ADR names.
- [x] An ADR records how the mechanism is chosen; `--extended-help` and `--ai-help` describe it; the password does not appear in `-v` output or `--log-file`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Decision (ADR-1315): the client names PLAIN first; when the broker refuses it and offers SCRAM, it reconnects with the strongest (SHA-512, then SHA-256). `--login-options` is not carried to extensions, so no flag. Wrong password: exit 67, `Kafka SASL SCRAM-SHA-256 authentication failed: ...`.
- Vectors: RFC 7677 SHA-256 pinned; SHA-512 vector worked out by an independent PowerShell script over the same BCL primitives. Brokers are scripted doubles (SaslHandshake + SaslAuthenticate); no live broker.
- `touches` widened to the Kafka test project, the Kafka library (client flow, handler, extension description) and one line pair in Conformance.Extended KafkaSaslTests (its refusal case offered SCRAM-SHA-256, now GSSAPI); no other Doing task names them. `--extended-help` carries the extension description, which now describes the logins; `--ai-help` has no Kafka text. The password is only hashed; no `-v` or log line carries it.
- Quality run: Kafka library 100% line and branch, complexity max 10. Fast tests green for Kafka, Conformance.Extended and Composition.

## Log

- 2026-11-20: Created from Surly bug reports (1942 #5).
- 2026-11-20: Backlog -> Doing. Criteria at claim: f6501668 5a0f3215 e63f3e90 3d836d5e.
- 2026-11-20: Doing -> Done. SCRAM-SHA-256 and SCRAM-SHA-512 logins built and tested
