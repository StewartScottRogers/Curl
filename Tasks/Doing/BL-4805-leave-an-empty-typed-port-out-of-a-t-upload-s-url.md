---
id: BL-4805
title: Leave an empty typed port out of a -T upload's URL
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Cli.UnitLibrary/UploadTransferUrl.cs, Curl.Cli.UnitTests/UploadTransferUrlTests.cs]
requirement: none
created: 2026-11-03
completed:
---
# BL-4805 — Leave an empty typed port out of a -T upload's URL

## Goal

`curl -w %{url} -T f.txt http://h:/` prints `http://h/f.txt`, as curl does, not `http://h:80/f.txt`.

## Context

Found by the shift-end review of 2026-11-03 (review-20261103-195556, code-reviewer and silent-failure passes) in the BL-4801 change.

`Curl.Cli.UnitLibrary/UploadTransferUrl.cs:74` now writes the port when `PortWasTyped(url.OriginalString)` is true. `PortWasTyped` (line 102) re-parses the original string and answers true for any colon after the host, including an empty port. `CurlUrlAuthority.CheckPort` (`Curl.Protocol.Abstractions.UnitLibrary/CurlUrlAuthority.cs:194`) accepts an empty port with a scheme and leaves `Port` null, so `CurlUrl.Port` becomes the scheme default (`CurlUrl.cs:50`) and `Format` writes `http://h:80/...`. curl's URL API drops an empty port, so the `%{url}` of the upload no longer matches curl.

The re-parse also duplicates what `CurlUrlAuthority.SplitPort` already decided and can disagree with it on other inputs (for example a scheme-less URL whose path holds `:/`). Prefer exposing whether a port was written from `CurlUrl` (it is `authority.Port is not null` in the constructor) and using that instead of `PortWasTyped` and `TrimAuthoritySlashes`. Measure real curl with `Record-CurlExchange.ps1` before pinning the output.

## Acceptance criteria

- [ ] `UploadTransferUrlTests` has a case for `http://h:/` whose expected URL is what real curl prints (measured, recorded in the test's comment), and it passes.
- [ ] The BL-4801 cases (`http://127.0.0.1:80/`, `http://[::1]:80/`, `https://u:p@h:443/d/?`, `http://h:80`) still keep the typed port.
- [ ] `UploadTransferUrl` no longer re-parses `OriginalString` to find the port, or a test shows each input where the re-parse and `CurlUrlAuthority.SplitPort` could disagree.
- [ ] `dotnet build` is clean, the fast tests pass and `Curl.Cli.UnitLibrary` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 2e60ad31 780d1012 4457cece bd306633.
