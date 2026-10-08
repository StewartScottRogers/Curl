---
id: BL-1727
title: Measure the environment variable and config file gap with Gap/Tools/Measure-EnvironmentGap.ps1
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1721, BL-1722]
touches: [Gap/Tools/Measure-EnvironmentGap.ps1, Gap/Tools/Fixtures/environment, Gap/Upstream/8.21.0/environment.json]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1727 — Measure the environment variable and config file gap with Gap/Tools/Measure-EnvironmentGap.ps1

## Goal

`Gap/Tools/Measure-EnvironmentGap.ps1` builds the `environment` upstream inventory, covering
every environment variable and config-file rule the release documents. It then measures each
one with a probe recipe through `Curl.Console` and the matched reference curl. Items with no
recipe yet are marked `unmeasured`.

## Context

This is ADR-0433 decision 2, area `environment`. Formats are in
`Gap/Instructions/Gap-Format.md` (BL-1720). Use `Get-UpstreamRelease.ps1` (BL-1721) and
`Invoke-GapProbe.ps1` (BL-1722). The probe's controlled environment points `HOME`,
`USERPROFILE`, `APPDATA`, `CURL_HOME` and `XDG_CONFIG_HOME` at an empty folder, and each
recipe adds what it needs.

**Inventory.**

- `docs/cmdline-opts/_ENVIRONMENT.md`: the curl tool's environment variables, of kind
  `variable`.
- `docs/libcurl/libcurl-env.md`: libcurl's, of kind `variable`. A variable named in both
  files is one item.
- `docs/cmdline-opts/config.md`: the config file's search locations, of kind `config-path`
  (one item per location the document names, in its order), and its syntax rules, of kind
  `config-syntax` (one item per rule the document states, for example the `=` or `:`
  separator, quoting and comments).

Read the three files' structure in the release. Do not assume it. Keys:
`environment:<NAME>` with the name's case as upstream writes it, `environment:config-path:<n>`,
and `environment:config-syntax:<slug>`.

**Recipes.** A recipe is a function that sets up an environment and maybe a config file,
runs `Invoke-GapProbe`, and compares the two binaries' exit code, stdout and stderr. For the
proxy variables it also compares the request bytes a loopback server received. Use
`Record-CurlExchange.ps1` for that: it binds a loopback port, records the request bytes and
writes stdout, stderr and the exit code. Run it once with `-Curl <reference>` and once with
`-Curl <candidate>`. Recipes are required for:

- `http_proxy`, `HTTPS_PROXY`, `ALL_PROXY` and `NO_PROXY`, plus any other case forms the
  documents name. The proxy is the loopback server, the URL is `http://gap.invalid/`, and
  the comparison is on whether, and how, the request reached the proxy.
- Every `config-path` item. The recipe puts a config file holding `write-out = "gap-config-hit"`
  in that location (for example `$CURL_HOME/.curlrc`, `$XDG_CONFIG_HOME/curlrc`,
  `$HOME/.curlrc`, and on Windows `%APPDATA%\_curlrc` or whatever the document names), and
  compares stdout for a `file://` transfer. Locations the document gives for another
  platform only are `excluded` with reason `other-platform`.
- Every `config-syntax` item the document states with an example.
- `-q` disabling the config file, keyed `environment:config-syntax:q-disables`.

Every other item is `unmeasured` with reason `no-recipe`. That counts against the score, as
ADR-0433 decision 2 says, and adding recipes later is how it narrows. Without a matching
reference, a recipe compares Curl with what the document states, where the document states
an observable result. Otherwise the item is `unmeasured` with `no-reference`.

**Parameters.** `-UpstreamRoot`, `-Version`, `-Candidate`, `-OutFile`, `-InventoryOnly`
(no probing and no Windows-only API; BL-1735 runs it on Linux), `-SelfTest` (fake
`_ENVIRONMENT.md`, `libcurl-env.md` and `config.md` under
`Gap/Tools/Fixtures/environment/`, with canned recipe results through `-ProbeResults`).

Commit `Gap/Upstream/8.21.0/environment.json`.

## Acceptance criteria

- [x] `Gap/Tools/Measure-EnvironmentGap.ps1 -SelfTest` prints `PASS` lines and no `FAIL` under Windows PowerShell 5.1 and PowerShell 7. It checks: a variable named in both documents is one item; config locations keep the document's order; equal recipe results give `match` and different ones give `gap`; an item with no recipe is `unmeasured` with `no-recipe`; another platform's location is `excluded` with `other-platform`.
- [x] `Gap/Upstream/8.21.0/environment.json` is committed and valid against `Gap-Format.md`.
- [x] A real run on Windows measures the four proxy variables and every config location through both binaries. Its counts, and the list of `no-recipe` items, are recorded in this task's Notes.
- [x] No recipe reads or writes the user's real `.curlrc`, `_curlrc` or environment: every one runs in the probe's temporary home.
- [x] The header help documents every parameter and lists the recipes. The script is ASCII only.

## Notes

- Real run on Windows (2026-10-08, reference curl 8.21.0 Schannel from Git for Windows,
  candidate Curl.Console Release build): match 24, gap 0, unmeasured 18, excluded 1,
  X/Y 24/42. http_proxy, HTTPS_PROXY, ALL_PROXY and NO_PROXY and config locations 1 to 6
  and 8 were measured through both binaries; location 7 (getpwuid) is excluded with
  platform:unix.
- no-probe items (18): APPDATA, COLUMNS, CURL_CA_BUNDLE, CURL_HOME, CURL_SSL_BACKEND,
  HOME, NETRC, QLOGDIR, SHELL, SSLKEYLOGFILE, SSL_CERT_DIR, SSL_CERT_FILE, USERPROFILE,
  XDG_CONFIG_HOME, [scheme]_proxy, [url-protocol]_PROXY, config-syntax:line-length-limit,
  config-syntax:one-option-per-line.
- Reasons follow Gap-Format.md: no-probe in place of no-recipe and no-reference, and
  platform:<os> in place of other-platform (ADR-0435).
- HTTPS_PROXY is probed with http://gap.invalid/ as the task says, so the comparison is
  that neither binary sends the request to the proxy (both exit 6). On Windows variable
  names are not case sensitive, so the lower and upper case forms are one variable.
- Location 8 (the executable's folder) runs copies of both binaries in a temporary
  folder; nothing is written beside an installed curl.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. Measure-EnvironmentGap.ps1 measures 42 environment items, 24 match, through both binaries
