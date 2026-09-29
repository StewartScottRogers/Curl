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
completed: 2026-09-28
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

- [x] The measurements are recorded in the ADR's Context, per build.
- [x] `Documentation/Planning/Decisions/ADR-<next free number>-<slug>.md` exists (number checked unused), Status Accepted, marked "Decided by Claude under Stewart's delegation", with a table: option, Schannel build, OpenSSL build, curl.se Windows build, Curl's route (`SslStream` or hand-built) per platform, and the text Curl prints; no cell says Curl refuses an option some build honours.
- [x] Consequences name BL-618 (parsing, `--engine`, `--dump-ca-embed` and whatever `SslStream` carries), BL-708 (routing), BL-709 to BL-714 as the implementations.
- [x] `Documentation/Planning/Decisions/README.md` indexes the new ADR.

## Notes

- Decision: ADR-0151 (0149 and 0150 were already taken on other lanes' branches). Every option is accepted on every platform; the hand-built client carries each row ADR-0140 already lists (no rows added or removed); where the platform's curl ignores or refuses an option, Curl prints the honouring build's text (curl.se's LibreSSL build on Windows where it honours the option, otherwise OpenSSL's).
- Measured with `Record-CurlExchange.ps1 -Tls -k` on mingw curl 8.21.0 (Schannel), curl.se's 8.18.0 (LibreSSL 4.2.1, WinGet) and Ubuntu 8.18.0 (OpenSSL 3.5.5, WSL); no measured build has `--ech` or `--ssl-sessions` (both need an optional libcurl feature), so their texts come from curl-8_21_0 source and are left for BL-710/BL-711 to pin.
- Choices taken: `--engine list` prints `<none>` everywhere (Curl has no engines; listing OpenSSL's `dynamic` would be false); `--engine <name>` ignored on Windows as the Schannel build, 53/66 elsewhere as the OpenSSL build; `--dump-ca-embed` prints nothing (no embedded bundle: Curl trusts the OS store, as both platform builds); TLS 1.0/1.1 offered on every platform through the hand-built client, as BL-714 already states.
- Written directly in the session rather than through align-and-document: the work was measurement plus one ADR, and the measurements lived in this session. No `.cs` or project file changed, so `verify` was not needed.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. ADR-0151 decides how all ten TLS options (plus --no-sessionid, --ssl-allow-beast, TLS 1.0/1.1) are honoured on every platform, from Schannel, curl.se and OpenSSL measurements
