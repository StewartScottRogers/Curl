---
id: BL-372
title: Refuse -O on an ipfs:// or ipns:// URL as curl 8.21.0 does
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-372 — Refuse -O on an ipfs:// or ipns:// URL as curl 8.21.0 does

## Goal

`-O` / `--remote-name` on an `ipfs://` or `ipns://` URL fails as curl 8.21.0 does, before any gateway is looked up.

## Context

- Found by BL-240 (IPFS rewrite wired into `CurlCommandRunner`). curl 8.21.0 takes the remote name from the URL before the IPFS rewrite and fails on it.
- Measured on curl 8.21.0 (mingw, Schannel) on 2026-09-27, with no gateway configured: `curl -w '[%{filename_effective}|%{exitcode}|%{errormsg}]\n' -O ipfs://bafyabc/n.txt` and `-O ipfs://bafyabc` both print `curl: Failed to extract a filename from the URL to use for storage` then `curl: (1) Unsupported protocol`, and `[|1|Unsupported protocol]`; exit 1. Measure with `--ipfs-gateway` given, and under `-s`, before pinning.

## Acceptance criteria

- [ ] Tests in `Curl.Console.UnitTests` pin the measured stderr, `-w` output and exit code for `-O ipfs://bafyabc/n.txt`, over fakes.
- [ ] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1 -IncludeIntegration` reports 100% line and branch coverage and no failing member for `Curl.Console`.

## Notes

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
