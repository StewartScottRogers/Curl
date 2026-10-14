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
completed: 2026-10-13
---
# BL-2137 — Re-measure the embedded option manual against curl 8.22.0

## Goal

`curl --help --<option>` and `curl --manual` print curl 8.22.0's manual text, byte for byte, for every option.

## Context

BL-2136 re-measured the `--help` pages (`Curl.Cli.UnitTests/HelpReference/help*.txt` except `help-option-*`) against the curl 8.22.0 Schannel reference build (`C:\Program Files\Git\ucrt64\bin\curl.exe` on the factory machine). It found the embedded manual (`CurlManual`, `CurlOptionManualSection`) is still curl 8.21.0's: `curl --help --xattr` in 8.22.0 adds the `user.xdg.referrer.url` sentence, so `help-option-xattr.txt` differs. `help-option-cert-status`, `-keepalive` and `-v` still match. Diff every option's section against 8.22.0 and update the manual text and fixtures; `--ai-help` derives option text from the same manual, so keep its tests right.

## Acceptance criteria

- [x] `help-option-xattr.txt` is re-measured with curl 8.22.0 and `CurlOptionManualSectionTests` passes against it.
- [x] A diff of `curl --help --<option>` from curl 8.22.0 against Curl's output for every option in `CurlHelpTable` is empty, recorded under Notes.
- [x] `CurlAiHelpTextTests` passes.

## Notes

- 2026-10-13: `CurlManual.txt` replaced with curl 8.22.0's `curl -M` (ucrt64 Schannel reference build):
  7983 lines, 305475 bytes with CR LF, SHA-256 `dd2789bd…9a61e7`; `--manual` and `COLUMNS=40` give the
  same bytes. Curl's `curl -M` now prints the same hash. `CurlManualTests` pins the new values.
- 8.22.0's manual adds sections for `--httpsig-algo`, `--httpsig-key`, `--httpsig-keyid` and
  `--httpsig-headers`; the reference build's `--help --httpsig-algo` prints "Incorrect option name"
  (the option is not in its option list), and so does Curl's.
- Diff: for every long name and letter in 8.22.0's `--help all` (333 subjects) plus `--no-verbose`,
  `--no-progress-meter`, `--no-xattr` and `--bogus-x` - 337 in all - curl 8.22.0 and `dotnet curl.dll`
  gave the same stdout SHA-256, the same stderr (bar the reference build's Debug-enabled warning) and the
  same exit code. Zero differed.
- `help-option-measurements.txt` (634 subjects) re-measured with 8.22.0: 21 subjects changed. The
  reference build is a debug build and knows the `DEBUGBUILD` options `--test-duphandle` and `--test-event`
  (prints nothing, no Incorrect line); their four rows keep a release build's `incorrect`, because
  `CurlOptionAliasTable` leaves `DEBUGBUILD` rows out on purpose. Recorded in the test's summary.
- `help-option-xattr.txt` re-measured (adds the `user.xdg.referrer.url` sentence); `-cert-status`,
  `-keepalive` and `-v` re-measured and unchanged. `CurlAiHelpTextTests`' `--data` expectation follows the
  8.22.0 wording ("Send the specified data to a server.").

## Log

- 2026-10-13: Created.
- 2026-10-13: Backlog -> Doing.
- 2026-10-13: Doing -> Done. Embedded manual is curl 8.22.0's; --help --<option> matches 8.22.0 for all 337 subjects, -M byte-identical
