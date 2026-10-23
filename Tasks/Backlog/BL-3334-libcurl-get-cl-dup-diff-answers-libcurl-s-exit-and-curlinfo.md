---
id: BL-3334
title: Libcurl get/cl-dup-diff answers libcurl's exit and CURLINFO_CONTENT_LENGTH_DOWNLOAD_T for two different Content-Length headers
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/Http1, Curl.Engine.UnitLibrary/TransferInfoFromReport.LengthAndCondition.cs]
requirement: none
created: 2026-10-23
completed:
---
# BL-3334 — Libcurl get/cl-dup-diff answers libcurl's exit and CURLINFO_CONTENT_LENGTH_DOWNLOAD_T for two different Content-Length headers

## Goal

Curl's Libcurl answers as libcurl 8.22.0 (Schannel) in easytest.py's get/cl-dup-diff, get-follow/cl-dup-diff and get-nobody/cl-dup-diff cases: the route (ContentLengthDuplicateDifferentRoute) sends `Content-Length: 1` and `Content-Length: 2`; libcurl answers CURLINFO_CONTENT_LENGTH_DOWNLOAD_T (0x60000F) -1, Curl answers 1.

## Context

Left by BL-3323. TransferInfoFromReport.ContentLengthOf already answers -1 when the report's `Content-Length` values differ, so the report the HTTP handler gives for this response seems to carry only the first length (or the transfer ends a way that bypasses it). First measure with LibcurlEasyDifferentialTests: libcurl's exit code and Curl's for these cases, and what the HTTP/1 handler puts on TransferReport.ResponseHeaders; libcurl 8.22.0 refuses differing duplicate lengths in lib/http.c (`Content-Length` parse, CURLE_WEIRD_SERVER_REPLY).

## Acceptance criteria

- [ ] The three cl-dup-diff cases are identical in LibcurlEasyDifferentialTests' report for 0x60000F (and the exit code), and a focused unit test pins each answer (ADR-0482).
- [ ] Projects build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 reports 0 failing members for the changed libraries.

## Notes

## Log

- 2026-10-23: Created.
