---
id: BL-2460
title: Engine: honour EasyHttpOptions.HttpGet so CURLOPT_HTTPGET after POSTFIELDS sends a GET
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-15
completed:
---
# BL-2460 — Engine: honour EasyHttpOptions.HttpGet so CURLOPT_HTTPGET after POSTFIELDS sends a GET

## Goal

With `EasyHttpOptions.HttpGet` true and `Post` false, `ManagedEasyTransfer` sends a `GET` with no body even when `PostFields` is set, as libcurl does after `CURLOPT_POSTFIELDS` then `CURLOPT_HTTPGET`.

## Context

ADR-0561 (BL-2406): Libcurl's `HTTPGET` setter sets `HttpGet` and clears `Post`, but keeps `PostFields`, because upstream keeps `postfields` and switches only its `method` field. `Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs` (around line 391) treats a non-null `PostFields` as a POST whatever `HttpGet` says. A later `POST` 1 or `POSTFIELDS` clears `HttpGet` again, so a `POST` stays a `POST`.

## Acceptance criteria

- [ ] A `Curl.Engine.UnitTests` test pins that options with `PostFields` set, `HttpGet` true and `Post` false send a `GET` with no body and no `Content-Length`.
- [ ] A test pins that `PostFields` set, with `HttpGet` false, still sends a `POST`.
- [ ] `dotnet build` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reports 0 failing members.

## Notes

## Log

- 2026-10-15: Created.
