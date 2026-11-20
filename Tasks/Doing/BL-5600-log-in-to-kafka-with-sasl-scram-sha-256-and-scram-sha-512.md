---
id: BL-5600
title: Log in to Kafka with SASL SCRAM-SHA-256 and SCRAM-SHA-512
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Kafka.UnitLibrary/Wire/KafkaSasl]
requirement: none
created: 2026-11-20
completed:
---
# BL-5600 — Log in to Kafka with SASL SCRAM-SHA-256 and SCRAM-SHA-512

## Goal

The `kafka://` and `kafkas://` client logs in with SCRAM-SHA-256 or SCRAM-SHA-512 when the broker offers it and `-u` is given.

## Context

From Surly cookbook bug report `2026-11-19T1942-Curl-bug-report.md` (batch 1 of the 1,000-recipe plan), finding #5. Build tested by the reports: the Curl port at `b9ca725077` (`curl`, `curl-mcp 0.1.0`, `Curl.Api 0.2.0`, `Libcurl 0.2.0`), Windows 11 (Linux and macOS through the cookbook's CI where stated); server Surl `763754785`. Check that it still reproduces on the current branch first.

The cookbook asked for SCRAM logins for mail and Kafka. Upstream curl has no SCRAM SASL mechanism for mail (its SASL list is LOGIN, PLAIN, CRAM-MD5, DIGEST-MD5, GSSAPI, EXTERNAL, NTLM, XOAUTH2 and OAUTHBEARER), so mail keeps curl's behaviour and gets no task. Kafka is an extension and SCRAM-SHA-256/512 is a standard Kafka SASL mechanism (KIP-84, RFC 5802 and RFC 7677): build it by hand with the BCL's `Rfc2898DeriveBytes` and `HMACSHA256`/`HMACSHA512`. Choose how a user picks the mechanism (offered-order preference, or `--login-options AUTH=SCRAM-SHA-512`) and record it in an ADR. The password never appears in `-v` or `--log-file`.

## Acceptance criteria

- [ ] The SCRAM client reproduces RFC 7677's SCRAM-SHA-256 test vector exactly, pinned by a unit test, and a SCRAM-SHA-512 vector likewise.
- [ ] Against a Kafka broker (or a test double speaking the SaslHandshake and SaslAuthenticate exchange) configured for SCRAM-SHA-256 and for SCRAM-SHA-512, `curl -u user:pass kafka://...` produces and consumes a message; a wrong password fails with the exit code the ADR names.
- [ ] An ADR records how the mechanism is chosen; `--extended-help` and `--ai-help` describe it; the password does not appear in `-v` output or `--log-file`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-20: Created from Surly bug reports (1942 #5).
- 2026-11-20: Backlog -> Doing. Criteria at claim: f6501668 5a0f3215 e63f3e90 3d836d5e.
