---
id: BL-2792
title: Fix flaky test AuthenticateAsClientAsync_WithCertTypeP12InTheSchannelBuild_PresentsTheCertificate
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitTests/SslStreamTlsProviderTests.cs]
requirement: none
created: 2026-10-19
completed:
---
# BL-2792 — Fix flaky test AuthenticateAsClientAsync_WithCertTypeP12InTheSchannelBuild_PresentsTheCertificate

## Goal

<!-- One sentence: the observable outcome once this task is done. -->

## Context

Failed once on Windows in BL-2669's fast run on 2026-10-19 (lane 2, with other lanes building): `schannel: failed to receive handshake, SSL/TLS connection failed` after 747 ms. BL-2669 changed nothing in Curl.Networking. Reproduce alone and under load; fix the race or the timeout.

<!-- Why this matters, and where to start: requirement IDs, ADRs, projects, files, upstream curl links with the curl version. -->

## Acceptance criteria

- [ ] <!-- A statement someone else can check from the repository without asking a question. -->

## Notes

## Log

- 2026-10-19: Created.
