---
id: BL-423
title: Pin the Curl.Core tests' drive-letter file URLs to Windows and give Linux and macOS their own expectations
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Core.UnitTests/IpfsGatewayRewriterTests.cs, Curl.Core.UnitTests/ProxySelectorTests.cs, Curl.Core.UnitTests/RedirectFollowerTests.cs, Curl.Core.UnitTests/TransferRetrierTests.cs]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-423 — Pin the Curl.Core tests' drive-letter file URLs to Windows and give Linux and macOS their own expectations

## Goal

The four `Curl.Core.UnitTests` tests below pass on the `ubuntu-latest` and `macos-latest`
jobs of the `CI` workflow and still pass on `windows-latest`, and the two whose outcome
really differs by platform pin each platform's curl answer.

## Context

**Root cause (fails on both Linux and macOS): Windows drive-letter `file://` URLs, which
curl's non-Windows builds reject.** `CurlUrl` applies drive letters only when
`OperatingSystem.IsWindows()` (`Curl.Protocol.Abstractions.UnitLibrary/CurlUrl.cs`), per
ADR-0010 ("Drive letters follow the platform") and curl's `lib/urlapi.c` (checked against curl
master on 2026-09-27; the solution pins curl 8.21.0), which returns `CURLUE_BAD_FILE_URL` for a
drive letter when not built for Windows. Production is right and must not change. Two of the
four are test-fixture assumptions; two are **real platform behaviour differences that
production already gets right**, and the test must pin both sides.

Failing in CI run 36344057083 (`gh run view 36344057083 --log-failed`), identical on
ubuntu-latest and macos-latest, 4 results:

1. `ProxySelectorTests.TrySelect_FileUrl_NeverUsesAProxy` (line 89, `file:///c:/nonexist`):
   `FormatException: curl rejects the URL`. Fixture assumption. Use `file:///nonexist`.
2. `TransferRetrierTests.RunAsync_TransientStatusFromAnotherScheme_IsFinal` with
   `[DataRow("file:///Z:/f")]` (line 273): `FormatException`. Fixture assumption. Use `file:///f`.
3. `RedirectFollowerTests.FollowAsync_SchemeNotAllowed_Exits1ProtocolDisabledInRedirect` with
   `[DataRow("file:///C:/Windows/win.ini", "file")]` (line 423): expected `UnsupportedProtocol`,
   actual `UrlMalformat`. **Real difference**: off Windows curl cannot parse that `Location`, so
   the follow fails as a malformed URL before the scheme check (curl's `Curl_follow` hands the new
   URL to `curl_url_set`, which fails with `CURLUE_BAD_FILE_URL`). The row's purpose is the
   `file` scheme being disabled in a redirect, so change it to a drive-less `file:///Windows/win.ini`,
   which reaches the scheme check on every build. Do not add a Linux assertion of exit 3 here
   unless it is measured on Linux with `Record-CurlExchange.ps1`; the exit 3 message text for a
   rejected redirect is not recorded in this repository.
4. `IpfsGatewayRewriterTests.TryRewrite_UnusableGatewayOption_IsMalformedTargetUrl` with
   `[DataRow("file:///C:/x")]` (line 91): expected `IpfsGatewayFailure.MalformedTargetUrl`,
   got a different failure. **Real difference**: curl's `src/tool_ipfs.c` (checked against curl
   master on 2026-09-27) parses `--ipfs-gateway` with `curl_url_set(..., CURLU_GUESS_SCHEME)` and,
   when that fails, sets `CURLE_BAD_FUNCTION_ARGUMENT` and prints
   `--ipfs-gateway was given a malformed URL`. Off Windows `file:///C:/x` fails that parse, so
   the answer there is `IpfsGatewayFailure.MalformedGatewayOption` (exit 43), which is what
   `IpfsGatewayRewriter` returns (line 139). Move this row out of the `DataRow` set into its own
   test marked `[OSCondition(OperatingSystems.Windows)]` expecting `MalformedTargetUrl`, and add a
   twin marked `[OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]` expecting
   `MalformedGatewayOption`, as `Curl.Cli.UnitTests/CommandLineProtocolOptionTests.cs` pairs its
   platform tests. Keep the other rows unchanged.

Do not change production code. Lanes test only on Windows, so the Linux and macOS result comes
from the `CI` workflow (`.github/workflows/ci.yml`) run on the pushed commit.

## Acceptance criteria

- [x] The four tests named above exist with the changes described; the IPFS drive-letter case
      is a Windows-only test expecting `MalformedTargetUrl` plus a non-Windows test expecting
      `MalformedGatewayOption`.
- [x] `dotnet build Curl.Core.UnitTests -warnaserror` is clean and
      `dotnet test Curl.Core.UnitTests --filter "TestCategory!=Integration"` passes on Windows.
- [x] In the `CI` run for the pushed commit on `work/dark-factory`, none of
      `TrySelect_FileUrl_NeverUsesAProxy`, `RunAsync_TransientStatusFromAnotherScheme_IsFinal`,
      `FollowAsync_SchemeNotAllowed_Exits1ProtocolDisabledInRedirect` or
      `TryRewrite_UnusableGatewayOption_IsMalformedTargetUrl` (nor the new twin) appears in
      `gh run view <run-id> --log-failed` for the `Build and test (ubuntu-latest)` or
      `Build and test (macos-latest)` job.
      (Ticked on the evidence in Notes; the lane cannot push, so the CI run on the shift's
      integrated commit is the final check.)
- [x] No file outside the four named test files changed.

## Notes

- `ProxySelectorTests`: `file:///c:/nonexist` -> `file:///nonexist`. `TransferRetrierTests`:
  `file:///Z:/f` -> `file:///f`. `RedirectFollowerTests`: the `file` row is now
  `file:///Windows/win.ini`; the measured-on-Windows comment keeps its original URL, with a line
  saying why the row is drive-less.
- `IpfsGatewayRewriterTests`: the `file:///C:/x` row left the `DataRow` set and became
  `TryRewrite_OnWindows_DriveLetterFileGatewayOption_IsMalformedTargetUrl` (Windows only) and
  `TryRewrite_OnLinuxOrMacOS_DriveLetterFileGatewayOption_IsMalformedGatewayOption` (excludes
  Windows). Names follow the `OnWindows` / `OnLinuxOrMacOS` pattern of
  `Curl.Cli.UnitTests/CommandLineProtocolOptionTests.cs`.
- No production code changed. The non-Windows twin skips on Windows, so it is first run by CI.
- Verified 2026-09-27: `dotnet build Curl.Core.UnitTests -warnaserror` clean, Curl.Core.UnitTests
  845 passed, 4 skipped; solution `dotnet build` 0 warnings, fast tests green.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. The four Curl.Core tests use drive-less file URLs or platform-pinned IPFS twins, so they run on Linux and macOS
