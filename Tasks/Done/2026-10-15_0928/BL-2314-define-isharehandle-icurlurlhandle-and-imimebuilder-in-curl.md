---
id: BL-2314
title: Define IShareHandle, ICurlUrlHandle and IMimeBuilder in Curl.Api.UnitLibrary
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2312]
touches: [Curl.Api.UnitLibrary/EngineContracts/ICurlEngine.cs, Curl.Api.UnitLibrary/EngineContracts/Share, Curl.Api.UnitLibrary/EngineContracts/Url, Curl.Api.UnitLibrary/EngineContracts/Mime, Curl.Api.UnitTests/EngineContracts/Share, Curl.Api.UnitTests/EngineContracts/Url, Curl.Api.UnitTests/EngineContracts/Mime, Curl.Api.UnitLibrary/Options/Behaviour/EasyBehaviourOptions.cs, Curl.Api.UnitTests/Options/Behaviour/EasyBehaviourOptionsTests.cs]
requirement: none
created: 2026-10-15
completed: 2026-10-15
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

- [x] The three interfaces and their enums exist in the named sub-folders, each member documented with the libcurl function or constant it stands for.
- [x] `ICurlEngine` has `CreateShare()`, `CreateUrl()` and `CreateMime()`.
- [x] Tests in `Curl.Api.UnitTests/EngineContracts/Share`, `/Url` and `/Mime` pin every upstream constant (`CURL_LOCK_*`, `CURLUPART_*`, `CURLU_*`, `CURLFORM_*`).
- [x] `dotnet build` is clean (warnings are errors).
- [x] `dotnet test --filter "TestCategory!=Integration"` passes.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged.

## Notes

- Plan: contracts only, after BL-2313's multi pattern. Share/ has `IShareHandle`, `CurlLockData`, `CurlLockAccess`, `ShareLockCallback`, `ShareUnlockCallback`; Url/ has `ICurlUrlHandle`, `CurlUrlPart`, `[Flags] CurlUrlFlags`; Mime/ has `IMimeBuilder`, `IMimePartBuilder`, `ICurlFormBuilder`, `CurlFormOption`, `CurlFormArgument`, `FormGetCallback`. `ICurlEngine` gains `CreateShare/CreateUrl/CreateMime` and `CreateForm` (curl_formadd has no init function). Decisions in ADR-0511.
- Reconciled `EasyBehaviourOptions.Share` from `object?` to `IShareHandle?`, as the Context asked. That needed `Options/Behaviour/EasyBehaviourOptions.cs` and its test file, which were outside `touches`; added both. The only other Doing task, BL-2289, touches Curl.Console/Curl.Engine only, so no overlap.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Api.UnitLibrary`: 100% line, 100% branch, 0 failing members. Curl.Api.UnitTests 393 passed; Libcurl.UnitTests has no tests yet.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. IShareHandle, ICurlUrlHandle, IMimeBuilder and ICurlFormBuilder contracts with upstream constants pinned; ICurlEngine creates each
