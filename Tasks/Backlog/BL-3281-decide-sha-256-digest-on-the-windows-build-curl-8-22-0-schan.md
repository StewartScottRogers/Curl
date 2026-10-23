---
id: BL-3281
title: Decide SHA-256 Digest on the Windows build: curl 8.22.0 Schannel fails it with exit 94 (differential auth-digest256)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Authentication.UnitLibrary/DigestAuthenticator.cs, Curl.Authentication.UnitLibrary/DigestAlgorithm.cs, Curl.Authentication.UnitTests]
requirement: none
created: 2026-10-23
completed:
---
# BL-3281 — Decide SHA-256 Digest on the Windows build: curl 8.22.0 Schannel fails it with exit 94 (differential auth-digest256)

## Goal

Curl's answer to a SHA-256 Digest challenge on Windows matches the platform's curl, decided in an ADR.

## Context

BL-3233 differential case auth-digest256 (-sS --digest -u u:p {U}/digest256): curl 8.22.0 (Schannel, SSPI digest) sends one request and exits 94 "An authentication function returned an error"; Curl answers the challenge and gets "sha256 ok". Measure whether the Windows inbox curl.exe does the same, then decide (match the platform's curl) and record an ADR; the OpenSSL builds on Linux and macOS answer SHA-256.

## Acceptance criteria

- [ ] An ADR records the decision; Curl's exit, stdout and wire for auth-digest256 on Windows match it, pinned by an OSCondition unit test, with the other platforms' answer pinned separately.
- [ ] The fast tests pass and Measure-CodeQuality.ps1 reports 0 failing members for each changed library.

## Notes

## Log

- 2026-10-23: Created.
