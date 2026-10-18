---
id: BL-2721
title: Gap office: measure the four TLS variables and config path 7 (getpwuid) on Linux and macOS
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-2720]
touches: [Gap/Tools/Measure-EnvironmentGap.ps1]
lane: no
requirement: none
created: 2026-10-18
completed:
---
# BL-2721 — Gap office: measure the four TLS variables and config path 7 (getpwuid) on Linux and macOS

## Goal

A gap-platforms run measures `environment:CURL_CA_BUNDLE`, `environment:SSL_CERT_FILE`, `environment:SSL_CERT_DIR`, `environment:SSLKEYLOGFILE` and `environment:config-path:7` as `match` or `gap` on Linux and macOS, so none of them stays `unmeasured` (`no-probe`).

## Context

- Gap scorecard 2026-10-17_2225: environment is 39 of 39 on Windows but 28 of 33 (84.8%) on Linux and macOS. Its five non-matching items are all `unmeasured` with reason `no-probe`, with no gap in Curl measured yet.
- `Gap/Tools/Measure-EnvironmentGap.ps1`'s `Get-Recipe` gives the four TLS variables a recipe only `if ($script:OnWindows)`, because `Record-CurlExchange.ps1 -Tls` needed a Windows `RSACng` key. BL-2720 lifts that. Move the four recipes out of the Windows-only switch. Off Windows, `--ssl-revoke-best-effort` is a Schannel option; check what the OpenSSL reference does with it, and drop it off Windows if it changes the answer.
- `config-path:7` is curl 8.22.0 `docs/cmdline-opts/config.md` item 7, "Non-Windows: use getpwuid to find the home directory", and has no recipe. A recipe needs a `.curlrc` in the passwd home with `CURL_HOME`, `XDG_CONFIG_HOME` and `HOME` removed. That home is the real user's home, so the recipe runs only when `GITHUB_ACTIONS` is set. It must save and restore any `.curlrc` already there. Off CI it stays `no-probe` and says why.
- Interactive only: the whole task is in the gap office's paths (ADR-0433). Dispatch with `gh workflow run gap-platforms.yml --ref <the branch with the change>` so the office's tools are the changed ones (BL-2522's lesson: a dispatch from `master` measures with `master`'s tools).
- Any item that measures `gap` becomes a gap finding the next gap run files, not part of this task.

## Acceptance criteria

- [ ] `Measure-EnvironmentGap.ps1`'s help and recipes cover the four TLS variables on every platform and `config-path:7` on CI, and `-SelfTest` passes.
- [ ] A gap-platforms run on the change is named under Notes with all five items' states on Linux and macOS, none `no-probe`.

## Notes

## Log

- 2026-10-18: Created.
