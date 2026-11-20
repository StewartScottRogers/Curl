---
id: BL-5607
title: Keep the query string on s3:// URLs so ?prefix= and other list parameters reach the server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.S3.UnitLibrary/Client/S3Endpoint.cs, Curl.Extended.Protocol.S3.UnitLibrary/Client/S3Operations.cs]
requirement: none
created: 2026-11-20
completed:
---
# BL-5607 — Keep the query string on s3:// URLs so ?prefix= and other list parameters reach the server

## Goal

`curl s3://bucket/?prefix=logs/` lists only the keys under `logs/`: the URL's query string reaches the S3 request (and its signature) instead of being dropped.

## Context

From Surly cookbook bug report `2026-11-20T0553-Curl-bug-report.md` (batch 3), finding #4. Build tested by the reports: the Curl port at `b9ca725077` (`curl`, `curl-mcp 0.1.0`, `Curl.Api 0.2.0`, `Libcurl 0.2.0`), Windows 11 (Linux and macOS through the cookbook's CI where stated); server Surl `763754785`. Check that it still reproduces on the current branch first.

`curl` drops the query string on `s3://`, so `?prefix=` cannot be used and the prefix must go in the path. Pass the query through to the ListObjectsV2 (or GetObject) request, canonically ordered and encoded for SigV4 (the signing code is in `Curl.Extended.Protocol.S3.UnitLibrary/Signing`), and decide what a path prefix plus a `prefix=` parameter mean together (ADR if not obvious). Update `--extended-help`/`--ai-help` for s3.

## Acceptance criteria

- [ ] Against an S3 test server (or SeaweedFS/Surl), `curl s3://bucket/?prefix=logs/` lists only keys under `logs/`, and `?max-keys=1` returns one key.
- [ ] The signed request's canonical query string is pinned by a unit test against a SigV4 example.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-20: Created from Surly bug reports (0553 #4).
