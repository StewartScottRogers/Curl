---
id: BL-4801
title: Keep a typed default port in %{url} for a -T upload whose file name is appended
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Cli.UnitLibrary/UploadTransferUrl.cs, Curl.Console/CurlCommandRunner.cs, Curl.Console.UnitTests/Af0623UploadWriteOutUrlTests.cs, Curl.Cli.UnitTests]
requirement: none
created: 2026-11-03
completed:
---
# BL-4801 — Keep a typed default port in %{url} for a -T upload whose file name is appended

## Goal

`curl -w "%{url}" -T sub/f.txt http://127.0.0.1:80/` prints `http://127.0.0.1:80/f.txt`, the port as typed, just as real curl does.

## Context

Found by the shift-end review of 2026-11-03 (ADR-0710), by the pr-review-toolkit code-reviewer, and verified against the code. The diff 77d5a82d9...4501875e6 (BL-4796, AF-0623) added `UrlWithUploadFileName` at `Curl.Console/CurlCommandRunner.cs:3423-3430`. When a `-T` file name is appended, `%{url}` now prints `transferUrl`. That string comes from `UploadTransferUrl.Format` (`Curl.Cli.UnitLibrary/UploadTransferUrl.cs:68-77`), which leaves out any port equal to the scheme's default, even a typed one: `CurlUrl.IsDefaultPort` is true for a typed `:80` (`Curl.Protocol.Abstractions.UnitLibrary/CurlUrl.cs:51`). `Format`'s own doc comment admits that curl keeps the port.

curl's `add_file_name_to_url` (src/tool_operhlp.c) rebuilds the URL with `curl_url_get(..., CURLU_DEFAULT_SCHEME)` and without `CURLU_NO_DEFAULT_PORT`, so a typed port stays. Before this diff the gap showed only in `%{url_effective}`. Now `%{url}` has it too, which breaks the drop-in byte match. Measure real curl (`Record-CurlExchange.ps1`, curl 8.22.0) before pinning the output.

## Acceptance criteria

- [ ] Real curl's `%{url}` (and `%{url_effective}`) for `-T sub/f.txt http://127.0.0.1:80/` and `https://127.0.0.1:443/` is measured and recorded in this task's Notes.
- [ ] Curl prints the same bytes: a typed default port is kept in the URL built for a `-T` upload, and a URL with no port typed still prints none.
- [ ] `Curl.Console.UnitTests/Af0623UploadWriteOutUrlTests.cs` has a case with a typed `:80`, and `Curl.Cli.UnitTests` has a focused test of the URL `UploadTransferUrl` resolves with a typed default port (ADR-0482).
- [ ] The doc comment on `UploadTransferUrl.Format` no longer says the port is left out, if it is now kept.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-03: Created.
