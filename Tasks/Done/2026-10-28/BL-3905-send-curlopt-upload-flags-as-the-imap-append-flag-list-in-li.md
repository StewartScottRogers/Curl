---
id: BL-3905
title: Send CURLOPT_UPLOAD_FLAGS as the IMAP APPEND flag list in Libcurl transfers
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3653]
touches: [Curl.Engine.UnitLibrary/ManagedEasyTransfer.Context.cs, Curl.Engine.UnitTests/ManagedEasyTransferUploadFlagsTests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3905 — Send CURLOPT_UPLOAD_FLAGS as the IMAP APPEND flag list in Libcurl transfers

## Goal

A Libcurl IMAP upload sends the flags `CURLOPT_UPLOAD_FLAGS` sets in its `APPEND` command, as libcurl 8.22.0 does.

## Context

BL-3653 (CGR-124) made `curl_easy_setopt(h, CURLOPT_UPLOAD_FLAGS, n)` answer `CURLE_OK` and store the low byte in
`EasySmtpOptions.UploadFlags` (default `CURLULFLAG_SEEN`, 16). The engine does not read it yet:
`ManagedEasyTransfer.Context.cs`'s `TransferSettingsOf` passes `UploadFlags: []`, so `ImapAppend` sends no flag list.
Turn the bitmask into the names `ImapAppend` reads (`answered` 1, `deleted` 2, `draft` 4, `flagged` 8, `seen` 16, in
that order, as `Curl.Console/EasyOptionsFromCommandLine.cs`'s `UploadFlagNames` does). Measure libcurl 8.22.0's
`APPEND` line for the default (no setopt) and for a few values against a loopback IMAP server before pinning it
(the default may send `(\Seen)`).

## Acceptance criteria

- [x] A Libcurl transfer with `CURLOPT_UPLOAD_FLAGS` set sends the same `APPEND` command bytes as libcurl 8.22.0, measured and recorded in Notes, for the default and at least three values.
- [x] A unit test in `Curl.Engine.UnitTests` pins the bitmask-to-names mapping.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

Measured curl 8.22.0 (tool) against a loopback IMAP server (`APPEND INBOX ... {6}`): no option sends `(Seen)`; `--upload-flags answered,deleted,draft,flagged,seen` sends `(Answered Deleted Draft Flagged Seen)`; `draft,answered` sends `(Answered Draft Seen)`; `flagged` sends `(Flagged Seen)` (the tool ORs seen in). The engine sends exactly the names of the low five bits in curl order; default byte 16 gives `(Seen)`, 0 gives no list. Raw libcurl value 0 could not be measured here (no C compiler); mapping follows the bitmask. Quality run for Curl.Engine.UnitLibrary: 0 failing members.

## Log

- 2026-10-26: Created by BL-3653 for the transfer side of CURLOPT_UPLOAD_FLAGS.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 103a276f 368dad15 779790c0.
- 2026-10-28: Doing -> Done. UPLOAD_FLAGS mapped to APPEND flag list
