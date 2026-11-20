---
id: BL-5638
title: Fill FileTime from MDTM in the FTP handler so Libcurl and -w filetime match curl
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Ftp.UnitLibrary, Curl.Original.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-11-20
completed:
---
# BL-5638 — Fill FileTime from MDTM in the FTP handler so Libcurl and -w filetime match curl

## Goal

With `CURLOPT_FILETIME` (`-R`, `--remote-time`, `FetchFileTime`) set, an `ftp://` transfer reports the `MDTM` time as the transfer's file time through the engine, so `Libcurl`'s `CURLINFO_FILETIME`, the console's `-w '%{filetime}'` and `Curl.Api`'s `Info.FileTime` all match curl.

## Context

Found by the shift-end review of 2026-11-20 (ADR-0710; code-reviewer pass over `7c1689d7d4...e89e78f09c`).

BL-5634 fixed the symptom only in `Curl.Api`: `Curl.Api/Messages/CurlClient.SendAsync.cs:57` and `WithFileTimeFromLastModified` (line 77 onward) re-parse the synthesized `Last-Modified` header when the engine left `FileTime` at -1. The cause is untouched: `Curl.Engine.UnitLibrary/TransferInfoFromReport.cs:36` takes `FileTime` from `result.SourceLastWriteUnixSeconds`, and nothing in `Curl.Original.Protocol.Ftp.UnitLibrary` ever sets `SourceLastWriteUnixSeconds` (grep finds none). So `Libcurl` and the console still report -1 for an FTP file time where libcurl reports the `MDTM` time, and `Curl.Api` now differs from `Libcurl` on the same transfer. Real curl fills `CURLINFO_FILETIME` from `MDTM` in `lib/ftp.c` (`ftp_state_mdtm_resp`).

Once the FTP handler sets it, the `Curl.Api` fallback becomes dead for FTP; decide whether to keep it (it then only differs from the engine on non-FTP schemes) or remove it with its tests, and record why in the task's Notes.

## Acceptance criteria

- [ ] The FTP handler sets the transfer result's `SourceLastWriteUnixSeconds` from a `213` `MDTM` reply when the file time is requested, pinned by a test in `Curl.Original.Protocol.Ftp.UnitTests`.
- [ ] An unparsable or failed `MDTM` leaves it unset (file time -1), pinned by a test.
- [ ] `Curl.Api`'s `WithFileTimeFromLastModified` is either removed with its now-redundant tests or kept with a stated reason in Notes.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing. Criteria at claim: 6cd003a7 1cf44c34 6f576b33 3d836d5e.
