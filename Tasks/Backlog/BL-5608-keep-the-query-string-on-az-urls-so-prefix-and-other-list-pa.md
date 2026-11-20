---
id: BL-5608
title: Keep the query string on az:// URLs so ?prefix= and other list parameters reach the server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.AzureStorage.UnitLibrary/Requests/AzureStorageUrlMapping.cs]
requirement: none
created: 2026-11-20
completed:
---
# BL-5608 — Keep the query string on az:// URLs so ?prefix= and other list parameters reach the server

## Goal

`curl az://account/container?prefix=logs/` lists only blobs under `logs/`: the URL's query string reaches the Azure request instead of being dropped, and is covered by the Shared Key signature.

## Context

From Surly cookbook bug report `2026-11-20T0553-Curl-bug-report.md` (batch 3), finding #4. Build tested by the reports: the Curl port at `b9ca725077` (`curl`, `curl-mcp 0.1.0`, `Curl.Api 0.2.0`, `Libcurl 0.2.0`), Windows 11 (Linux and macOS through the cookbook's CI where stated); server Surl `763754785`. Check that it still reproduces on the current branch first.

`curl` drops the query string on `az://`, so `?prefix=` cannot be used and the prefix must go in the path. Pass the query through to the List Blobs (or blob) request; with Shared Key (BL-5569) the canonicalized resource must include the query parameters as the spec says, and a SAS query in the URL must still be kept apart from the list parameters. Update `--extended-help`/`--ai-help` for az.

## Acceptance criteria

- [ ] Against Azurite (or Surl's Azure emulation), `curl az://.../container?prefix=logs/` lists only blobs under `logs/`, with anonymous, SAS and Shared Key credentials.
- [ ] The Shared Key canonicalized resource with query parameters is pinned by a unit test against the spec's example.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-20: Created from Surly bug reports (0553 #4).
