---
id: BL-2709
title: Make upstream case 694 pass: CURLINFO_HTTPAUTH_USED keeps the scheme an earlier perform of the handle picked
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2635]
touches: [Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs, Curl.Engine.UnitLibrary/TransferInfoFromReport.cs, Curl.Engine.UnitTests/ManagedEasyTransferTests.cs, Curl.Engine.UnitTests/TransferInfoFromReportTests.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2709 — Make upstream case 694 pass: CURLINFO_HTTPAUTH_USED keeps the scheme an earlier perform of the handle picked

## Goal

Upstream case 694 passes through the runner and holds the ratchet.

## Context

Found by BL-2635, which made the libtest engine send curl's own NTLM type-1 message (`EngineDialing.UsesHandBuiltNtlm`). Measured on Windows after it, test694's requests all match, but its stdout differs: `differs at byte 785 (line 23): expected the end, got "CURLINFO_HTTPAUTH_USED did not say NTLM\n"`. lib694 performs twice on one handle: the first answers NTLM (401, type-2, 200), the second goes to `/path/6940010` on the same NTLM-authenticated connection, sends no `Authorization` and gets a 200 without a challenge. curl 8.22.0 still reports `CURLAUTH_NTLM` there, because `data->state.authhost.picked` lives on the handle and `Curl_pretransfer` resets only `want` (lib/transfer.c; `Curl_http_output_auth` sets `picked` from `want` only when none is picked). Curl's `ManagedEasyTransfer` builds each perform's `HttpRequestOptions.AuthSchemePicked` fresh (`None`), so the second report's `AuthSchemePicked` is `None` and `TransferInfoFromReport` gives `HttpAuthUsed` 0. Start at `ManagedEasyTransfer.PerformParsedAsync` / `CreateContext`: carry the last perform's `TransferReport.AuthSchemePicked`, masked by the handle's wanted schemes, into the next perform's `AuthSchemePicked` (`Reset` clears it). Check `HttpProtocolHandler.AllowedSchemesOf`: a carried Basic or Digest is then sent before a challenge, as libcurl does; verify that against upstream cases with two performs on one handle before landing it.

## Acceptance criteria

- [ ] Upstream case 694 passes through `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` on Windows and is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] A focused unit test in `Curl.Engine.UnitTests` shows a second perform on one handle reporting the scheme the first picked as `Info.HttpAuthUsed` (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-10-18: touches narrowed from Curl.Engine.UnitLibrary, Curl.Engine.UnitTests to files, by reading ManagedEasyTransfer.PerformParsedAsync/CreateContext and TransferInfoFromReport, so it can run beside other Engine tasks.

## Log

- 2026-10-17: Created.
