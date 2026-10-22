---
id: BL-2819
title: Carry libcurl CURLOPT_USERNAME and CURLOPT_PASSWORD as credentials and bounce rtspd replies with swsbounce (from BL-2788's stash c61946edb9a51c86eef042dca7d5ca72bfc7d3ac)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/TransferContextFactory.cs, Curl.Engine.UnitTests/TransferContextFactoryTests.cs, Curl.Conformance.UnitLibrary/RtspServerConnection.cs, Curl.Conformance.UnitTests/RtspServerConnectorTests.cs]
requirement: none
created: 2026-10-19
completed: 2026-10-19
---
# BL-2819 — Carry libcurl CURLOPT_USERNAME and CURLOPT_PASSWORD as credentials and bounce rtspd replies with swsbounce (from BL-2788's stash c61946edb9a51c86eef042dca7d5ca72bfc7d3ac)

## Goal

A libcurl easy handle with CURLOPT_USERNAME and CURLOPT_PASSWORD carries them as credentials, and the conformance rtspd answers a second request with <data1> when the case has swsbounce, so upstream case 3100 can reach its retry.

## Context

Split from BL-2788 (cost cap). The work is done in stash c61946edb9a51c86eef042dca7d5ca72bfc7d3ac : `git diff c61946edb9a51c86eef042dca7d5ca72bfc7d3ac^1 c61946edb9a51c86eef042dca7d5ca72bfc7d3ac -- Curl.Engine.UnitLibrary Curl.Engine.UnitTests Curl.Conformance.UnitLibrary Curl.Conformance.UnitTests` (TransferContextFactory.CredentialsOf, test Create_LibcurlUserNameAndPassword_CarriesThemAsCredentials; RtspServerConnection swsbounce, test ReplyWithSwsbounce_... in RtspServerConnectorTests). Apply those files, then run the fast tests: the engine credential change may move other libcurl cases on the ratchet.

## Acceptance criteria

- [x] Create_LibcurlUserNameAndPassword_CarriesThemAsCredentials in Curl.Engine.UnitTests passes.
- [x] An RtspServerConnectorTests test shows a swsbounce case's second request answered with <data1>.
- [x] Curl.Engine.UnitLibrary and Curl.Conformance.UnitLibrary keep their quality gates (Measure-CodeQuality.ps1 for each).
- [x] dotnet build is clean and the fast tests pass, ratchet included.

## Notes

- The engine half had already landed: BL-2714 added `TransferContextFactory.CredentialsOf` over `UserName`, `Password` and `UserPassword`, so the stash's production change to `TransferContextFactory.cs` was dropped (it conflicted) and only its test applied. BL-2714 gives a password alone credentials (`:password`), as libcurl's `override_login` sets `user_passwd` for either; the stash's "CURLOPT_PASSWORD alone" row expected none, so it now expects `:password`, and a "none of the three" row pins `null`.
- The stash's swsbounce test had real newlines inside its string literals; written as escaped line feeds.
- Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary,Curl.Conformance.UnitLibrary: 0 failing members. Fast tests: 42 projects passed, 0 failed.

## Log

- 2026-10-19: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: 45448344 509f6632 3c186a8b e4e9ea6c.
- 2026-10-19: Doing -> Done. Libcurl USERNAME/PASSWORD credentials pinned by a TransferContextFactory test, and the conformance rtspd answers the request after a swsbounce reply with the next part
