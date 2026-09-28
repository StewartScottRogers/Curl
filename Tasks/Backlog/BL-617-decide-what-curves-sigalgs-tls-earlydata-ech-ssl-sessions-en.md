---
id: BL-617
title: Decide what --curves, --sigalgs, --tls-earlydata, --ech, --ssl-sessions, --engine, --dump-ca-embed and TLS-SRP options do per platform
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed:
---
# BL-617 — Decide what --curves, --sigalgs, --tls-earlydata, --ech, --ssl-sessions, --engine, --dump-ca-embed and TLS-SRP options do per platform

## Goal

An ADR states, for each of `--curves`, `--sigalgs`, `--tls-earlydata`, `--ech`, `--ssl-sessions`, `--engine`, `--dump-ca-embed`, `--tlsuser`, `--tlspassword` and `--tlsauthtype`, what the Schannel and OpenSSL curl 8.21.0 builds print and exit with, and what Curl does on each platform given what `SslStream` can control.

## Context

- Conformance audit 2026-09-28, row 18 (Major; "most are one refusal each on Schannel").
- Rules: ADR-0009 (match the platform's build), ADR-0011 (cipher options follow Schannel on Windows and are honoured elsewhere, the precedent for "honour where the BCL can"). `SslStream` has no API for curves, signature algorithms, early data, ECH, session export, engines or SRP, so off Windows a refusal that differs from the OpenSSL build may be unavoidable; the ADR states the chosen output for each and why.
- Measure all ten with `Record-CurlExchange.ps1 -Tls -k` on Windows and on Linux or macOS (with a plausible value each: `--curves X25519`, `--sigalgs ECDSA+SHA256`, `--engine list`, `--dump-ca-embed`, `--tlsuser u --tlspassword p`), recording stdout, stderr and exit code.

## Acceptance criteria

- [ ] The measurements are recorded in the ADR's Context, per platform.
- [ ] `Documentation/Planning/Decisions/ADR-<next free number>-<slug>.md` exists (number checked unused), Status Accepted, marked "Decided by Claude under Stewart's delegation", with a table: option, Schannel build, OpenSSL build, Curl on Windows, Curl elsewhere.
- [ ] Consequences name BL-618 as the implementation.
- [ ] `Documentation/Planning/Decisions/README.md` indexes the new ADR.

## Notes

## Log

- 2026-09-28: Created.
