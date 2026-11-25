---
id: BL-5661
title: Give Curl.Api an FTP resume offset and a names-only listing option
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Api/Options/Protocol/CurlFtpOptions.cs]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-5661 — Give Curl.Api an FTP resume offset and a names-only listing option

## Goal

A `Curl.Api` user can resume an FTP download or upload from an offset (`CURLOPT_RESUME_FROM_LARGE`, curl's `-C`) and ask for a names-only listing (`CURLOPT_DIRLISTONLY`, curl's `-l`), without sending `REST` through `PreQuote`.

## Context

From Surly cookbook bug report `2026-11-20T2225-Curl-bug-report.md` (batch 7 of the 1,000-recipe plan), finding 4. Build tested: Curl `7d167fa10a`, Windows 11; server Surl `9ad60d80c`. Check it still reproduces on the current branch first.

`Curl.Api` has no resume option for FTP: the cookbook's C# recipe sends `REST` through `PreQuote` and sets `IgnoreContentLength`. It also has no names-only listing (`NLST`). The engine already supports both (the console's `-C` and `-l`); expose them in `Curl.Api`, decide whether the resume offset sits in `CurlFtpOptions` or applies to every protocol as libcurl's does (HTTP Range, FILE, SFTP too), and document both in the README.

## Acceptance criteria

- [x] A `Curl.Api` FTP download with a resume offset of N sends `REST N` and returns the file from byte N, pinned by a test; an upload with an offset appends as `-C` does.
- [x] A names-only listing sends `NLST` and returns the names, pinned by a test.
- [x] The README documents both; `dotnet build` is clean and the fast tests pass.

## Notes

Decision: ADR-1325, options on CurlFtpOptions (ResumeFrom, ListOnly), applied to the transfer options by CurlProtocolOptions. touches widened to CurlProtocolOptions.cs, the tests, README and the ADR. The engine already sends REST N and NLST for these transfer options (console -C and -l); Curl.Api tests pin that the options reach the handle. Curl.Api quality run: 0 failing members. CI on Linux and macOS: tests are platform-neutral.

## Log

- 2026-11-20: Created from Surly bug report 2026-11-20T2225, finding 4.
- 2026-11-20: Backlog -> Doing. Criteria at claim: 77aaecde b51ba9bf fc48aace.
- 2026-11-20: Doing -> Done. FTP ResumeFrom and ListOnly on CurlFtpOptions
