---
id: BL-2111
title: Gap environment: probe CURL_CA_BUNDLE, SSL_CERT_FILE, SSL_CERT_DIR and SSLKEYLOGFILE over a loopback TLS server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Gap/Tools]
lane: no
requirement: none
created: 2026-10-12
completed: 2026-10-12
---
# BL-2111 — Gap environment: probe CURL_CA_BUNDLE, SSL_CERT_FILE, SSL_CERT_DIR and SSLKEYLOGFILE over a loopback TLS server

## Goal

The four TLS environment variables are measured against the reference over a loopback TLS server.

## Context

Gap run 2026-10-11_1950 scored environment 24/42 (57.1%): every measured item matches, and 18 are unmeasured with reason no-probe because `Gap/Tools/Measure-EnvironmentGap.ps1` has no recipe for them. Gap paths are interactive only; the work lands through a pull request from a branch built from master, merged once CI is green on Windows, Linux and macOS. Each new recipe runs through the reference curl and Curl.Console as the existing ones do (`Record-CurlExchange.ps1` for the loopback server), and the script's -SelfTest covers it. A recipe that finds a real difference is a gap, filed as a lane task by the next gap run, not hidden.

## Acceptance criteria

- [x] `CURL_CA_BUNDLE`, `SSL_CERT_FILE`, `SSL_CERT_DIR` and `SSLKEYLOGFILE` each have a recipe over a loopback TLS server with a throwaway certificate, or an exclusion with a reason where the Schannel reference ignores the variable (measured, not assumed).
- [x] `Measure-EnvironmentGap.ps1 -SelfTest` passes, and a local run measures none of the four as no-probe.

## Notes

## Log

- 2026-10-12: Created.
- 2026-10-12: Backlog -> Doing. interactive: gap environment probes
- 2026-10-12: Doing -> Done. PR #124 merged: recipes or exclusions for all 18 no-probe environment items
