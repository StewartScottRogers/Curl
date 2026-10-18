---
id: BL-2727
title: Make Libcurl answer CURLOPT_PROTOCOLS_STR, CURLOPT_REDIR_PROTOCOLS_STR, CURLOPT_SSH_HOST_PUBLIC_KEY_MD5 and CURLINFO_SIZE_DELIVERED as libcurl 8.22.0 does
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Libcurl, Libcurl.UnitTests, Curl.Conformance.LibTest.UnitTests]
requirement: none
created: 2026-10-18
completed: 2026-10-18
---
# BL-2727 — Make Libcurl answer CURLOPT_PROTOCOLS_STR, CURLOPT_REDIR_PROTOCOLS_STR, CURLOPT_SSH_HOST_PUBLIC_KEY_MD5 and CURLINFO_SIZE_DELIVERED as libcurl 8.22.0 does

## Goal

`curl_easy_setopt` and `curl_easy_getinfo` in `Libcurl` give curl 8.22.0's answers for the four calls the lib1521 port (BL-2642) found wrong, so upstream case 1521 prints only `ok`.

## Context

Found by running BL-2642's `Lib1521` (every option and info, `mk-lib1521.pl`) through case 1521. Libcurl's wrong answers today, against curl 8.22.0's `lib/setopt.c` and `lib/getinfo.c`:

- `CURLOPT_PROTOCOLS_STR` and `CURLOPT_REDIR_PROTOCOLS_STR` given `"string"` answer 43 (`CURLE_BAD_FUNCTION_ARGUMENT`); upstream's `protocol2num` answers 1 (`CURLE_UNSUPPORTED_PROTOCOL`) for a name no scheme has, 43 only for a list naming no protocol (such as `""` or `","`), and `CURLE_OK` for `all` (case-insensitive). Given `NULL` they answer 43; upstream answers `CURLE_OK` and resets to the default (`CURLPROTO_64ALL`, and `CURLPROTO_REDIR` for the redirect set).
- `CURLOPT_SSH_HOST_PUBLIC_KEY_MD5` given `"string"` answers 43; upstream stores any string with `Curl_setstropt` and checks the 32 hex digits only when it connects, so setopt answers `CURLE_OK` (the check belongs at connect time, where curl answers it).
- `CURLINFO_SIZE_DELIVERED` (`CURLINFO_OFF_T + 71`, 0x600047, `data->progress.deliver`: the body bytes delivered to the write callback) is missing from `CurlInfo`, so `curl_easy_getinfo` answers 48 (`CURLE_UNKNOWN_OPTION`); upstream answers `CURLE_OK` with an off_t.

Start in `Libcurl/Options/Network/EasyNetworkOptionSetters.cs` (`SetProtocols`, `SetRedirectProtocols`), the SSH option setters under `Libcurl/Options/Protocols`, and `Libcurl/Info/CurlInfo.cs` with its readers.

## Acceptance criteria

- [x] `CURLOPT_PROTOCOLS_STR` and `CURLOPT_REDIR_PROTOCOLS_STR` answer `CURLE_UNSUPPORTED_PROTOCOL` for an unknown scheme name, `CURLE_OK` for `NULL` (resetting to the default) and for `all`, and `CURLE_BAD_FUNCTION_ARGUMENT` for a list naming no protocol, each pinned by a test in `Libcurl.UnitTests`.
- [x] `CURLOPT_SSH_HOST_PUBLIC_KEY_MD5` answers `CURLE_OK` for any string at setopt time, pinned by a test, with the 32-hex-digit check still made where the transfer uses it.
- [x] `CurlInfo.SizeDelivered` (0x600047) exists and `curl_easy_getinfo` answers it `CURLE_OK` with a `long`, pinned by a test.
- [x] `Measure-CodeQuality.ps1 -Library Libcurl` reports 0 failing members.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

- Measured against upstream's own lib1597 table (`Lib1597.Cases`): any unknown name in the list, even after known ones (`http,http,ft`), is `CURLE_UNSUPPORTED_PROTOCOL`; empty entries are skipped; no entry at all (`""`, `",,"`) is 43; `NULL` and `all` are OK. The old "at least one known scheme" rule was wrong.
- `Curl.Conformance.LibTest.UnitTests` added to `touches` (no other task in Doing): `Lib1597_RunsTheStringsThroughTheHandleAndWritesTheCount` compared Libcurl's run against a fake that always answered 43; with correct answers and an engine listing http and https it now exits 0 with `Tested 15 strings`.
- `CURLINFO_SIZE_DELIVERED` reads `TransferInfo.SizeDownload`: the engine's downloaded size is the body bytes handed to the write callback. `TransferInfo` (Curl.Api.UnitLibrary) has no separate delivered count, and a transfer that decodes content differs only in curl's wire-versus-delivered split.
- `CURLOPT_SSH_HOST_PUBLIC_KEY_MD5` is stored as given; `SshHostKeyChecker` compares it with the key's fingerprint at connect time, so a wrong string fails the transfer there.

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: a6b0587e c36cc153 d16a21f8 208a64e8 4272a6b9.
- 2026-10-18: Doing -> Done. Libcurl answers PROTOCOLS_STR, REDIR_PROTOCOLS_STR, SSH_HOST_PUBLIC_KEY_MD5 and SIZE_DELIVERED as 8.22.0 does
