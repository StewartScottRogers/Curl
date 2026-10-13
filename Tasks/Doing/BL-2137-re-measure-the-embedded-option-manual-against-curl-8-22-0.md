---
id: BL-2137
title: Re-measure the embedded option manual against curl 8.22.0
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-10-13
completed:
---
# BL-2137 — Re-measure the embedded option manual against curl 8.22.0

## Goal

`curl --help --<option>` and `curl --manual` print curl 8.22.0's manual text, byte for byte, for every option.

## Context

BL-2136 re-measured the `--help` pages (`Curl.Cli.UnitTests/HelpReference/help*.txt` except `help-option-*`) against the curl 8.22.0 Schannel reference build (`C:\Program Files\Git\ucrt64\bin\curl.exe` on the factory machine). It found the embedded manual (`CurlManual`, `CurlOptionManualSection`) is still curl 8.21.0's: `curl --help --xattr` in 8.22.0 adds the `user.xdg.referrer.url` sentence, so `help-option-xattr.txt` differs. `help-option-cert-status`, `-keepalive` and `-v` still match. Diff every option's section against 8.22.0 and update the manual text and fixtures; `--ai-help` derives option text from the same manual, so keep its tests right.

## Acceptance criteria

- [ ] `help-option-xattr.txt` is re-measured with curl 8.22.0 and `CurlOptionManualSectionTests` passes against it.
- [ ] A diff of `curl --help --<option>` from curl 8.22.0 against Curl's output for every option in `CurlHelpTable` is empty, recorded under Notes.
- [ ] `CurlAiHelpTextTests` passes.

## Notes

## Log

- 2026-10-13: Created.
- 2026-10-13: Backlog -> Doing.
