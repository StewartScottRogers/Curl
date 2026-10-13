---
id: BL-2110
title: Gap environment: exclude QLOGDIR and CURL_SSL_BACKEND, and probe config locations, config syntax, NETRC and proxy name templates
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
# BL-2110 — Gap environment: exclude QLOGDIR and CURL_SSL_BACKEND, and probe config locations, config syntax, NETRC and proxy name templates

## Goal

Fourteen of the 18 unmeasured environment items get a recipe or a stated exclusion, so the next gap run measures them.

## Context

Gap run 2026-10-11_1950 scored environment 24/42 (57.1%): every measured item matches, and 18 are unmeasured with reason no-probe because `Gap/Tools/Measure-EnvironmentGap.ps1` has no recipe for them. Gap paths are interactive only; the work lands through a pull request from a branch built from master, merged once CI is green on Windows, Linux and macOS. Each new recipe runs through the reference curl and Curl.Console as the existing ones do (`Record-CurlExchange.ps1` for the loopback server), and the script's -SelfTest covers it. A recipe that finds a real difference is a gap, filed as a lane task by the next gap run, not hidden.

## Acceptance criteria

- [x] `QLOGDIR` and `CURL_SSL_BACKEND` are excluded with a `reference-lacks:<feature>` reason (HTTP3, MultiSSL), not unmeasured.
- [x] `HOME`, `USERPROFILE`, `APPDATA`, `CURL_HOME` and `XDG_CONFIG_HOME` each have a recipe placing a config file where that variable points.
- [x] `config-syntax:one-option-per-line` and `config-syntax:line-length-limit` each have a recipe.
- [x] `NETRC` has a recipe: a netrc file it names, and a loopback request whose credentials are compared.
- [x] `[scheme]_proxy` and `[url-protocol]_PROXY` are measured through concrete names (at least `ftp_proxy` and `HTTPS_PROXY`).
- [x] `Measure-EnvironmentGap.ps1 -SelfTest` passes, and a local run measures each of these items as match, gap or excluded - none no-probe.

## Notes

## Log

- 2026-10-12: Created.
- 2026-10-12: Backlog -> Doing. interactive: gap environment probes
- 2026-10-12: Doing -> Done. PR #124 merged: recipes or exclusions for all 18 no-probe environment items
