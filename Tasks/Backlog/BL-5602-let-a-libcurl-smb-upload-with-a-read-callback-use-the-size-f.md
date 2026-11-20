---
id: BL-5602
title: Let a Libcurl SMB upload with a read callback use the size from Infilesize or InfilesizeLarge
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Smb.UnitLibrary/SmbFileTransfer.cs, Libcurl/Options/Transfer]
requirement: none
created: 2026-11-20
completed:
---
# BL-5602 — Let a Libcurl SMB upload with a read callback use the size from Infilesize or InfilesizeLarge

## Goal

A `Libcurl` SMB upload set up as in C (`Upload = 1L`, a read callback, and `Infilesize` or `InfilesizeLarge`, in either order) uploads the file instead of stopping with exit 55 "SMB upload needs to know the size up front".

## Context

From Surly cookbook bug report `2026-11-19T2203-Curl-bug-report.md` (batch 2), finding #3. Build tested by the reports: the Curl port at `b9ca725077` (`curl`, `curl-mcp 0.1.0`, `Curl.Api 0.2.0`, `Libcurl 0.2.0`), Windows 11 (Linux and macOS through the cookbook's CI where stated); server Surl `763754785`. Check that it still reproduces on the current branch first.

`Upload = 1L` with a read callback and `Infilesize` or `InfilesizeLarge` (either order) stops with 55, "SMB upload needs to know the size up front" (the text is in `Curl.Original.Protocol.Smb.UnitLibrary/SmbMessages.cs`). The size is set, so it is not reaching the SMB handler on this path; `curl -T file smb://...` works because the console knows the file's length. Find where `Libcurl` passes `CURLOPT_INFILESIZE(_LARGE)` to the transfer and make the SMB handler see it whatever order the options were set in. Compare with curl 8.22.0: upstream SMB also needs the size up front, so a callback upload with no size set should still fail the way curl's does.

## Acceptance criteria

- [ ] A `Libcurl` test uploads over SMB to a test server with a read callback and `InfilesizeLarge` set before `Upload`, and again set after it; both store the bytes and return `Ok`.
- [ ] The same with `Infilesize` works too.
- [ ] A callback upload with no size set fails as curl 8.22.0's does, with the same `CurlCode` and text.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-20: Created from Surly bug reports (2203 #3).
