---
id: BL-4382
title: Re-measure GF-0097's environment recipes on linux: Curl at master no longer crashes with an empty SSL_CERT_DIR or SSL_CERT_FILE
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: []
lane: no
requirement: none
created: 2026-10-30
completed: 2026-10-31
---
# BL-4382 — Re-measure GF-0097's environment recipes on linux: Curl at master no longer crashes with an empty SSL_CERT_DIR or SSL_CERT_FILE

## Goal

The next linux gap measurement of GF-0097's items (`environment:SSL_CERT_DIR`, `environment:SSL_CERT_FILE`, `environment:[url-protocol]_PROXY`) is taken against Curl at master, and any remaining difference is traced to Curl or to the measuring recipe.

## Context

- BL-4380 (re-close of GF-0097) found no crash to fix: BL-2822's fix (679fee848, 76e7d599c, ADR-0707) is on origin/master since 2026-10-19, and a self-contained linux-x64 build of master run in WSL Ubuntu with `CURL_CA_BUNDLE= SSL_CERT_FILE= SSL_CERT_DIR=/etc/ssl/certs`, `CURL_CA_BUNDLE= SSL_CERT_DIR= SSL_CERT_FILE=/etc/ssl/certs/ca-certificates.crt` and `CURL_CA_BUNDLE= SSL_CERT_DIR= SSL_CERT_FILE= https_proxy=http://127.0.0.1:9` raised no exception (exit 0, 0 and 7).
- BL-4380's evidence still quotes the original finding's exit 134. Its third criterion expects `request CONNECT dc.services.visualstudio.com:443`, a host Curl was never asked for. That is Application Insights, the .NET tooling's telemetry endpoint, so the measuring harness's own `dotnet` process most likely sends its telemetry through the recipe's `https_proxy`.
- A lane may not read or change the gap tool (ADR-0433), so only an interactive session can do this task. Remove `lane: no` only if the work turns out to need no gap path.

## Acceptance criteria

- [x] The gap tool's recipes for the three items are run on linux against Curl at master, and the output Curl gives for each one is recorded under Notes.
- [x] Any CONNECT the harness's own processes send through the recipe's proxy (dc.services.visualstudio.com) is kept out of the measured request, or a Notes line records why it is not.
- [x] Every remaining difference that lies in Curl is filed as a lane task.

## Notes

- 2026-10-31 (interactive): gap-platforms run 41219522943 (`ref=master`, `request=bl-4382`, both jobs success) measured `environment:SSL_CERT_DIR`, `environment:SSL_CERT_FILE`, `environment:[url-protocol]_PROXY` and `environment:CURL_CA_BUNDLE` as `match` on linux and on macos; the environment area is 33 match, 0 gap on both. Curl at master no longer crashes with an empty SSL_CERT_DIR or SSL_CERT_FILE.
- The `CONNECT dc.services.visualstudio.com:443` came from the harness: `Invoke-RecordedRun` (Gap/Tools/Measure-EnvironmentGap.ps1) sets HTTPS_PROXY for its own process, and the Record-CurlExchange.ps1 pwsh it starts sent its startup telemetry (Application Insights) through the recorder's port under the recipe's empty HOME, so the recorder took pwsh's CONNECT and real curl's timed out behind it (exit 28). It does not happen on every run (this one matched). Fixed on the gap branch, a6e4bda37, PR #261: the recorder starts with POWERSHELL_TELEMETRY_OPTOUT, POWERSHELL_UPDATECHECK=Off, DOTNET_CLI_TELEMETRY_OPTOUT and DOTNET_NOLOGO set; self-test 25/25 under Windows PowerShell and pwsh.
- No difference lies in Curl, so no lane task is filed. BL-4383 (all three variables empty gives exit 60, not curl's 0) stays as filed.


## Log

- 2026-10-30: Created.
- 2026-10-31: Backlog -> Doing. Interactive re-measure Criteria at claim: 9830ffd1 464471e8 90e650d0.
- 2026-10-31: Doing -> Done. Run 41219522943: all three items match on linux and macos; the proxy item's foreign CONNECT was pwsh telemetry, fixed in gap PR #261
