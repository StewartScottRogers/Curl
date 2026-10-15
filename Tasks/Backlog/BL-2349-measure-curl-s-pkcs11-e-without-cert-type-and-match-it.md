---
id: BL-2349
title: Measure curl's pkcs11: -E without --cert-type and match it
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-2324]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-15
completed:
---
# BL-2349 — Measure curl's pkcs11: -E without --cert-type and match it

## Goal

Measure real curl (Schannel and OpenSSL builds) with `-E pkcs11:...` and `--key pkcs11:...` and no `--cert-type`/`--key-type` (curl's tool sets `ENG` then), and make `EasyOptionsFromCommandLine.TlsOf` and `ClientCertificateLoader` match it. ADR-0530 dropped the `ENG` guess so BL-2324 changed no behaviour.

<!-- One sentence: the observable outcome once this task is done. -->

## Context

<!-- Why this matters, and where to start: requirement IDs, ADRs, projects, files, upstream curl links with the curl version. -->

## Acceptance criteria

- [ ] <!-- A statement someone else can check from the repository without asking a question. -->

## Notes

## Log

- 2026-10-15: Created.
