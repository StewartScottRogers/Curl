---
id: BL-2853
title: Decide whether Curl ships curl-config, the question behind upstream cases 1013, 1014, 1022 and 1023 (ADR)
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Wiki/Glossary.md]
requirement: none
created: 2026-10-19
completed:
---
# BL-2853 — Decide whether Curl ships curl-config, the question behind upstream cases 1013, 1014, 1022 and 1023 (ADR)

## Goal

One ADR, "Decided by Claude under Stewart's delegation", says whether Curl ships a `curl-config` (the helper script upstream installs beside `curl`), under which rule and in what form, so BL-2854 can implement it and run upstream cases 1013, 1014, 1022 and 1023.

## Context

Upstream cases 1013 and 1014 ("Compare curl --version with curl-config --protocols / --features") and 1022 and 1023 ("... --version / --vernum") run `curl --version` and then a postcheck `%PERL %SRCDIR/libtest/test1013.pl ../curl-config %LOGDIR/stdout<n> <protocols|features>` or `test1022.pl ... <version|vernum>`; the harness skips them because "Curl does not ship" `../curl-config`. The gap office has them `unmeasured | harness-unsupported`. Upstream's `curl-config` (docs/curl-config.md in curl 8.22.0, https://curl.se/docs/manpage.html lists `curl`'s options; `curl-config.1` is the sibling page) prints `--ca`, `--cc`, `--cflags`, `--checkfor [version]`, `--configure`, `--feature`, `--help`, `--libs`, `--prefix`, `--protocols`, `--ssl-backends`, `--static-libs`, `--version`, `--vernum` and `--built-shared`.

The standing rule for such questions (root CLAUDE.md, "Decisions") is to match the platform's curl: the Schannel build on Windows (ADR-0018: the mingw 8.21.0 build is the Windows reference), the OpenSSL build on Linux and macOS, and "the default answer to every question is what a complete reimplementation of curl in C# needs". Measure the facts first and put them in the ADR: whether the reference curl on each platform installs a `curl-config` (Linux distribution `libcurl-dev` packages and Homebrew do; the curl.se Windows zip does not, verify on the reference build), what each option answers on a build that has no C compiler flags to report (`--cflags`, `--libs`, `--static-libs`, `--cc`, `--configure`, `--ca`, `--prefix`), and what `Libcurl` (ADR-0506; the programming interface that replaces libcurl's C ABI) reports through `curl_version_info`. Then decide: (a) ship `curl-config` on all platforms, (b) ship it where the platform's curl does, or (c) not ship it - and if (c), how cases 1013, 1014, 1022 and 1023 are still run (for example the harness supplies the in-process answer, named in the ADR as a stand-in). Whatever is decided, name the executable's project and its place in `Curl.slnx` under the repository layout rules (flat, alphabetical, AOT, held to the four quality gates), and every option's answer on every platform.

## Acceptance criteria

- [ ] The ADR records, with the commands run, what the reference curl's `curl-config` is on Windows, Linux and macOS (or that it does not exist there) and which curl version was checked.
- [ ] The ADR decides (a), (b) or (c), gives the executable's project name and the exact answer of each `curl-config` option on each platform, and says what the harness does for cases 1013, 1014, 1022 and 1023.
- [ ] `Documentation/Wiki/Glossary.md` has a `curl-config` entry that states what it does.
- [ ] BL-2854 is edited (in Backlog, keeping its criteria checkable) so its Goal, `touches` and criteria match the ADR.

## Notes

## Log

- 2026-10-19: Created.
