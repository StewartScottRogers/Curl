---
id: BL-5607
title: Keep the query string on s3:// URLs so ?prefix= and other list parameters reach the server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.S3.UnitLibrary/Client/S3Endpoint.cs, Curl.Extended.Protocol.S3.UnitLibrary/Client/S3Operations.cs, Curl.Extended.Protocol.S3.UnitLibrary/Client/S3ProtocolHandler.cs, Curl.Extended.Protocol.S3.UnitLibrary/CLAUDE.md, Curl.Extended.Protocol.S3.UnitTests/Client/S3ProtocolHandlerTests.cs, Curl.Extended.Protocol.S3.UnitTests/Client/S3EndpointTests.cs, Documentation/Conformance/Extended/S3.md]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-5607 — Keep the query string on s3:// URLs so ?prefix= and other list parameters reach the server

## Goal

`curl s3://bucket/?prefix=logs/` lists only the keys under `logs/`: the URL's query string reaches the S3 request (and its signature) instead of being dropped.

## Context

From Surly cookbook bug report `2026-11-20T0553-Curl-bug-report.md` (batch 3), finding #4. Build tested by the reports: the Curl port at `b9ca725077` (`curl`, `curl-mcp 0.1.0`, `Curl.Api 0.2.0`, `Libcurl 0.2.0`), Windows 11 (Linux and macOS through the cookbook's CI where stated); server Surl `763754785`. Check that it still reproduces on the current branch first.

`curl` drops the query string on `s3://`, so `?prefix=` cannot be used and the prefix must go in the path. Pass the query through to the ListObjectsV2 (or GetObject) request, canonically ordered and encoded for SigV4 (the signing code is in `Curl.Extended.Protocol.S3.UnitLibrary/Signing`), and decide what a path prefix plus a `prefix=` parameter mean together (ADR if not obvious). Update `--extended-help`/`--ai-help` for s3.

## Acceptance criteria

- [x] Against an S3 test server (or SeaweedFS/Surl), `curl s3://bucket/?prefix=logs/` lists only keys under `logs/`, and `?max-keys=1` returns one key.
- [x] The signed request's canonical query string is pinned by a unit test against a SigV4 example.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Decided ADR-1314: query sent on GET and list; list `prefix` param appends to path prefix; `delimiter` replaces default. touches widened to the handler, tests and docs. Criterion 1 is met by handler tests with a stand-in server (no real S3 server in a lane); criterion 2 by the existing S3RequestSignerTests List Objects example (`max-keys=2&prefix=J`) plus new handler requests. Quality run: 0 failing members.

## Log

- 2026-11-20: Created from Surly bug reports (0553 #4).
- 2026-11-20: Backlog -> Doing. Criteria at claim: 70e0638f 8106b76c 3d836d5e.
- 2026-11-20: Doing -> Done. s3:// URL query string (?prefix=, ?max-keys=) reaches the signed S3 request
