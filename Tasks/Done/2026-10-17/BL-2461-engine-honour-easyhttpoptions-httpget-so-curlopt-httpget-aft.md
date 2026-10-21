---
id: BL-2461
title: Engine: honour EasyHttpOptions.HttpGet so CURLOPT_HTTPGET after POSTFIELDS sends a GET
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-16
---
# BL-2461 — Engine: honour EasyHttpOptions.HttpGet so CURLOPT_HTTPGET after POSTFIELDS sends a GET

## Goal

With `EasyHttpOptions.HttpGet` true and `Post` false, `ManagedEasyTransfer` sends a `GET` with no body even when `PostFields` is set, as libcurl does after `CURLOPT_POSTFIELDS` then `CURLOPT_HTTPGET`.

## Context

ADR-0565 (BL-2406): Libcurl's `HTTPGET` setter sets `HttpGet` and clears `Post`, but keeps `PostFields`, because upstream keeps `postfields` and switches only its `method` field. `Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs` (around line 391) treats a non-null `PostFields` as a POST whatever `HttpGet` says. A later `POST` 1 or `POSTFIELDS` clears `HttpGet` again, so a `POST` stays a `POST`.

## Acceptance criteria

- [x] A `Curl.Engine.UnitTests` test pins that options with `PostFields` set, `HttpGet` true and `Post` false send a `GET` with no body and no `Content-Length`.
- [x] A test pins that `PostFields` set, with `HttpGet` false, still sends a `POST`.
- [x] `dotnet build` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reports 0 failing members.

## Notes

- `ManagedEasyTransfer.WithoutPostFieldsUnderHttpGet` drops `PostFields` from the options a perform runs with when `HttpGet` is set and `Post` is not, so the HTTP handler sends a GET with no body; `CURLINFO_CONTENT_LENGTH_UPLOAD` then reads -1, as no post is made. Applied to every perform (the curl tool never sets `HttpGet`). Tests: `PerformAsync_PostFieldsThenHttpGet_SendsAGetWithNoBody`, `PerformAsync_PostFieldsWithoutHttpGet_SendsAPost`, `WithoutPostFieldsUnderHttpGet_HttpGetWithPost_KeepsThePostFields`. Measure-CodeQuality: 0 failing members.

## Log

- 2026-10-15: Created.
- 2026-10-16: Backlog -> Doing. Criteria at claim: 6e00b714 348f0d70 a618ea01.
- 2026-10-16: Doing -> Done. CURLOPT_HTTPGET after CURLOPT_POSTFIELDS now sends a GET with no body from a library handle
