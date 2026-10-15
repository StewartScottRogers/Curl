---
id: BL-2314
title: Define IShareHandle, ICurlUrlHandle and IMimeBuilder in Curl.Api.UnitLibrary
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2312]
touches: [Curl.Api.UnitLibrary/EngineContracts/ICurlEngine.cs, Curl.Api.UnitLibrary/EngineContracts/Share, Curl.Api.UnitLibrary/EngineContracts/Url, Curl.Api.UnitLibrary/EngineContracts/Mime, Curl.Api.UnitTests/EngineContracts/Share, Curl.Api.UnitTests/EngineContracts/Url, Curl.Api.UnitTests/EngineContracts/Mime]
requirement: none
created: 2026-10-15
completed:
---
# BL-2314 — Define IShareHandle, ICurlUrlHandle and IMimeBuilder in Curl.Api.UnitLibrary

## Goal

`Curl.Api.UnitLibrary/EngineContracts` defines `IShareHandle` (in `Share/`), `ICurlUrlHandle` (in `Url/`) and `IMimeBuilder` (in `Mime/`), and `ICurlEngine` gains `CreateShare()`, `CreateUrl()` and `CreateMime()`.

## Context

ADR-0506; plan sections "Share", "URL" and "MIME and formadd"; needs BL-2312. Upstream: curl 8.22.0's `include/curl/curl.h` (share, mime, formadd), `include/curl/urlapi.h`. Contracts only; implementations are phase 3.

- `IShareHandle`: `Share`/`Unshare` of a data kind enum (`CURL_LOCK_DATA_COOKIE`, `DNS`, `SSL_SESSION`, `CONNECT`, `PSL`, `HSTS`), lock and unlock callbacks (`CURLSHOPT_LOCKFUNC`/`UNLOCKFUNC`, `CURL_LOCK_ACCESS_*`), results as `CurlShareCode`. `EasyBehaviourOptions.Share` (the `CURLOPT_SHARE` property, BL-2294) refers to it by an opaque share identity if that type is needed before this task; reconcile here if so.
- `ICurlUrlHandle`: `Get(part, flags)` and `Set(part, value, flags)` returning `CurlUrlCode`, `Duplicate()`; part enum `CURLUPART_*`, `[Flags]` enum `CURLU_*` with upstream values. It will be implemented over Curl's existing `CurlUrl` parser; the name avoids clashing with it (ADR-0506).
- `IMimeBuilder`: builds the `EasyMimeBody` defined in `Curl.Api.Options.Http` (BL-2299): add part, name, file name, type, encoder (`binary`, `8bit`, `7bit`, `base64`, `quoted-printable`), data, file data, data callback, sub-parts, headers - `curl_mime_*`'s operations - each returning `CurlCode`; and a formadd builder (`CURLFORM_*` options, `CurlFormAddCode`) producing the same body.

## Acceptance criteria

- [ ] The three interfaces and their enums exist in the named sub-folders, each member documented with the libcurl function or constant it stands for.
- [ ] `ICurlEngine` has `CreateShare()`, `CreateUrl()` and `CreateMime()`.
- [ ] Tests in `Curl.Api.UnitTests/EngineContracts/Share`, `/Url` and `/Mime` pin every upstream constant (`CURL_LOCK_*`, `CURLUPART_*`, `CURLU_*`, `CURLFORM_*`).
- [ ] `dotnet build` is clean (warnings are errors).
- [ ] `dotnet test --filter "TestCategory!=Integration"` passes.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged.

## Notes

## Log

- 2026-10-15: Created.
