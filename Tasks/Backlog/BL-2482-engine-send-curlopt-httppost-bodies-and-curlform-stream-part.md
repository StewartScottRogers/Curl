---
id: BL-2482
title: Engine: send CURLOPT_HTTPPOST bodies and CURLFORM_STREAM parts through the read callback
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2470]
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-16
completed:
---
# BL-2482 — Engine: send CURLOPT_HTTPPOST bodies and CURLFORM_STREAM parts through the read callback

## Goal

A perform from the options with `EasyHttpOptions.HttpPost` (`CURLOPT_HTTPPOST`) posts that form as curl 8.22.0 does, a `CURLFORM_STREAM` part's content read through the handle's `CURLOPT_READFUNCTION` with the stream object as its argument.

## Context

BL-2440 (ADR-0618) built `ManagedFormBuilder`: `Build` gives the `EasyMimeBody` `Curl_getformdata` makes, and `Libcurl`'s `CURLOPT_HTTPPOST` setter stores it on `EasyHttpOptions.HttpPost`. Nothing in `Curl.Engine.UnitLibrary` reads `HttpPost` yet (`ManagedEasyTransfer` builds only `MimePost` through `MimePostForm`), and a `CURLFORM_STREAM` part's `Data` is `null`, because the read callback is the easy handle's (upstream passes `data->state.fread_func` to `Curl_getformdata`). Sending a callback part needs BL-2470 first. Start at `MimePostForm.BuildAsync` and `FormPost.DataOf`.

## Acceptance criteria

- [ ] A perform with `HttpPost` set and no `MimePost` sends the form body, compared with the tool's `-F` request for the same fields, in `ManagedEasyTransferTests`.
- [ ] A `CURLFORM_STREAM` field's bytes come from `EasyCallbackOptions.Read`, `CURLFORM_CONTENTSLENGTH` as its size (0 is unknown, sent chunked), pinned by a test.
- [ ] `dotnet build` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reports 0 failing members.
- [ ] `Curl.Engine.UnitLibrary/CLAUDE.md` is kept true.

## Notes

## Log

- 2026-10-16: Created.
