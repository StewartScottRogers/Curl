---
id: BL-617
title: Decide how --curves, --sigalgs, --tls-earlydata, --ech, --ssl-sessions, --engine, --dump-ca-embed and the TLS-SRP options are honoured on every platform
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-695]
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed:
---
# BL-617 — Decide how --curves, --sigalgs, --tls-earlydata, --ech, --ssl-sessions, --engine, --dump-ca-embed and the TLS-SRP options are honoured on every platform

## Goal

An ADR states, for each of `--curves`, `--sigalgs`, `--tls-earlydata`, `--ech`, `--ssl-sessions`, `--engine`, `--dump-ca-embed`, `--tlsuser`, `--tlspassword` and `--tlsauthtype` (and `--no-sessionid`, `--ssl-allow-beast` and TLS 1.0/1.1 where the OS refuses them), what the Schannel, OpenSSL and curl.se official builds of curl print and do, and how Curl honours it on each platform: through `SslStream` where it can, through the hand-built TLS client (BL-695) where it cannot. No option is refused on a platform because `SslStream` lacks a control.

## Context

- Conformance audit 2026-09-28, row 18 (Major).
- Standing rule (root `CLAUDE.md`, "Decisions", Stewart 2026-09-28): if any official curl build supports a feature, Curl supports it on every platform; output text still matches the platform's curl where both do the same thing; the ADR decides HOW, never WHETHER. ADR-0009 (match the platform's build) and ADR-0011 (cipher options) still govern the text. `SslStream` has no API for curves, signature algorithms, early data, ECH, session export, SRP, session-ID suppression or the BEAST split; BL-695's ADR fixes the hand-built client and its routing rule, and this ADR adds one routing row per option.
- `--engine` (OpenSSL engines; `--engine list` prints the build's engines) and `--dump-ca-embed` (prints the CA bundle embedded at build time) are honoured as the corresponding official build does: decide what Curl lists and whether it embeds a CA bundle (and which one, and how it is refreshed), with the text measured from a build that supports each.
- Measure all ten with `Record-CurlExchange.ps1 -Tls -k` on Windows (the Schannel build and curl.se's official LibreSSL build) and on Linux or macOS (with a plausible value each: `--curves X25519`, `--sigalgs ECDSA+SHA256`, `--engine list`, `--dump-ca-embed`, `--tlsuser u --tlspassword p`), recording stdout, stderr and exit code.

## Acceptance criteria

- [ ] The measurements are recorded in the ADR's Context, per build.
- [ ] `Documentation/Planning/Decisions/ADR-<next free number>-<slug>.md` exists (number checked unused), Status Accepted, marked "Decided by Claude under Stewart's delegation", with a table: option, Schannel build, OpenSSL build, curl.se Windows build, Curl's route (`SslStream` or hand-built) per platform, and the text Curl prints; no cell says Curl refuses an option some build honours.
- [ ] Consequences name BL-618 (parsing, `--engine`, `--dump-ca-embed` and whatever `SslStream` carries), BL-708 (routing), BL-709 to BL-714 as the implementations.
- [ ] `Documentation/Planning/Decisions/README.md` indexes the new ADR.

## Notes

## Log

- 2026-09-28: Created.
