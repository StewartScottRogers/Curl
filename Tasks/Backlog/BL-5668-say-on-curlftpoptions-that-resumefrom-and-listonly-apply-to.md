---
id: BL-5668
title: Say on CurlFtpOptions that ResumeFrom and ListOnly apply to every protocol, or move them to a transfer record
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Api/Options/Protocol/CurlFtpOptions.cs, Curl.Api/README.md, Curl.Api.UnitTests/Options/Protocol]
requirement: none
created: 2026-11-21
completed:
---
# BL-5668 — Say on CurlFtpOptions that ResumeFrom and ListOnly apply to every protocol, or move them to a transfer record

## Goal

`CurlFtpOptions.ResumeFrom` and `ListOnly` say what they do: either they apply to FTP transfers only, or they move to a transfer-wide record whose name and docs say they apply to every protocol.

## Context

- `Curl.Api/Options/Protocol/CurlFtpOptions.cs:94-112` and `Curl.Api/Options/Protocol/CurlProtocolOptions.cs:38` (ADR-1325). Found by the shift-end review's type-design-analyzer.
- `ApplyTo` writes `ResumeFrom` and `DirListOnly` into the shared `EasyTransferOptions`, which the engine reads for every protocol (`Curl.Engine.UnitLibrary/ManagedEasyTransfer.SetUp.cs:150`, `CallbackUploadResume.cs:35`). A client with `Ftp = new() { ResumeFrom = 500 }` therefore sends `Range: bytes=500-` on every HTTP request and resumes HTTP uploads too; `ListOnly` also reaches SFTP and SCP. A name on the FTP record that changes HTTP is a misalignment under CLAUDE.md's "say what it does, do what it says".
- Also undocumented: a request's `Ftp` record replaces the client's whole, so `ResumeFrom` resets to 0 when a request sets only `FileMethod`; and `-1` on a download is silently treated as no resume.

## Acceptance criteria

- [ ] Setting `CurlFtpOptions.ResumeFrom` no longer changes an HTTP transfer, or the option lives on a record whose name and doc comment say it is transfer-wide (record the choice in an ADR marked "Decided by Claude under Stewart's delegation").
- [ ] The doc comments say that a request's record replaces the client's whole and what `-1` does on a download.
- [ ] `Curl.Api/README.md` matches.
- [ ] A unit test in `Curl.Api.UnitTests` pins the chosen behaviour for an HTTP request on a client whose resume offset is set.
- [ ] `dotnet build` is clean and the fast tests pass; `Curl.Api` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-21: Created by the shift-end review (ADR-0710) of 8c81a2532a...1f330a1f28.
