---
id: BL-2136
title: Fix CI failure test1462 on Linux and macOS and Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests, Curl.Conformance.UnitLibrary, Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-10-13
completed: 2026-10-13
---
# BL-2136 — Fix CI failure test1462 on Linux and macOS and Windows

## Goal

`test1462` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `test1462` failed on Linux and macOS and Windows in CI run 38431484503 (https://github.com/StewartScottRogers/Curl/actions/runs/38431484503). First failing commit: 48919fca.

    Assertion failed.

Lanes test only on Windows, so reproduce with `gh run view 38431484503 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `test1462` passes locally, is back on `PassingUpstreamCases.txt`, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Re-measured every `--help` page fixture in `Curl.Cli.UnitTests/HelpReference` with the curl 8.22.0 mingw Schannel build (`C:\Program Files\Git\ucrt64\bin\curl.exe`), CR stripped. Changes: new `mqtt  MQTT protocol` category (`--help mqtt` lists `-d` and `--max-filesize`), `-d, --data` reads "Post data", and twenty options gained categories (ftp: `--max-filesize`, `-i`, `--ssl`, `--ssl-reqd`; http: `--abstract-unix-socket`, `--basic`, `--max-filesize`, `--oauth2-bearer`, `--proxy-header`, `-J`, `-i`, `--unix-socket`; imap/smtp: `--form-escape`; imap/ldap/pop3/smtp: `--sasl-authzid`, `--sasl-ir`; imap: `-T`, `--upload-flags`; upload: `--upload-flags`; scp/sftp: `--key`, `--knownhosts`, `--pass`; sftp: `--compressed-ssh`). No option was added or removed from `--help all`.
- `CurlHelpCategories.Mqtt` takes bit 12 (alphabetical after Ldap, as curl numbers them); later categories shift up one bit. `--ai-help` lists categories from `CurlHelpText.Categories`, so it gains `mqtt` with no other change; its `--data` summary now reads "Post data".
- `curl -h` is now 1291 bytes with CR LF (measured), was 1290.
- The per-option manual is still 8.21.0's (`--help --xattr` differs); filed as BL-2137, not widened here.
- No new branch or method, so Measure-CodeQuality was not rerun; the change is table data and fixtures.
- CI on Linux and macOS is checked by the shift's CI watch after integration; the fixtures and expectations are platform-neutral (LF text, no paths).

## Log

- 2026-10-13: Created.
- 2026-10-13: Cause (lane 1, BL-2134 integration): BL-2126 re-vendored upstream test data at curl-8_22_0, whose `--help category` lists a new `mqtt  MQTT protocol` row. Curl.Cli still prints curl 8.21.0's help pages. test1462 was taken off `PassingUpstreamCases.txt` so the combined tree is green; put it back once fixed. Fix: add `CurlHelpCategories.Mqtt` and its row in `CurlHelpText.Categories`, and re-measure every `Curl.Cli.UnitTests/HelpReference` fixture against curl 8.22.0 - the usage page also lists `mqtt` and `-d, --data` now reads "Post data", so diff all of them, not only the category list. Keep `--ai-help` right.
- 2026-10-13: Backlog -> Doing.
- 2026-10-13: Doing -> Done. Curl's --help pages match curl 8.22.0 (mqtt category, Post data, new category rows); test1462 back on the ratchet
