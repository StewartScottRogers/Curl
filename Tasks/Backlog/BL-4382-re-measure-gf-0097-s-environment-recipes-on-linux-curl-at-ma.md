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
completed:
---
# BL-4382 — Re-measure GF-0097's environment recipes on linux: Curl at master no longer crashes with an empty SSL_CERT_DIR or SSL_CERT_FILE

## Goal

The next linux gap measurement of GF-0097's items (`environment:SSL_CERT_DIR`, `environment:SSL_CERT_FILE`, `environment:[url-protocol]_PROXY`) is taken against Curl at master, and any remaining difference is traced to Curl or to the measuring recipe.

## Context

- BL-4380 (re-close of GF-0097) found no crash to fix: BL-2822's fix (679fee848, 76e7d599c, ADR-0707) is on origin/master since 2026-10-19, and a self-contained linux-x64 build of master run in WSL Ubuntu with `CURL_CA_BUNDLE= SSL_CERT_FILE= SSL_CERT_DIR=/etc/ssl/certs`, `CURL_CA_BUNDLE= SSL_CERT_DIR= SSL_CERT_FILE=/etc/ssl/certs/ca-certificates.crt` and `CURL_CA_BUNDLE= SSL_CERT_DIR= SSL_CERT_FILE= https_proxy=http://127.0.0.1:9` raised no exception (exit 0, 0 and 7).
- BL-4380's evidence still quotes the original finding's exit 134. Its third criterion expects `request CONNECT dc.services.visualstudio.com:443`, a host Curl was never asked for. That is Application Insights, the .NET tooling's telemetry endpoint, so the measuring harness's own `dotnet` process most likely sends its telemetry through the recipe's `https_proxy`.
- A lane may not read or change the gap tool (ADR-0433), so only an interactive session can do this task. Remove `lane: no` only if the work turns out to need no gap path.

## Acceptance criteria

- [ ] The gap tool's recipes for the three items are run on linux against Curl at master, and the output Curl gives for each one is recorded under Notes.
- [ ] Any CONNECT the harness's own processes send through the recipe's proxy (dc.services.visualstudio.com) is kept out of the measured request, or a Notes line records why it is not.
- [ ] Every remaining difference that lies in Curl is filed as a lane task.

## Notes



## Log

- 2026-10-30: Created.
