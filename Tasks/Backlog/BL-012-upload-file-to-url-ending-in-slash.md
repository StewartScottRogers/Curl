---
id: BL-012
title: Handle -T/--upload-file to a URL ending in /
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
requirement: none
created: 2026-09-25
completed:
---
# BL-012 — Handle `-T`/`--upload-file` to a URL ending in `/`

## Goal

When `-T`/`--upload-file` targets a URL ending in `/`, the local file's base name is
appended to the URL before the transfer is dispatched.

## Context

This is a command-line-layer concern in `Curl.Cli.UnitLibrary`. It is resolved before
`ITransferContext` is constructed, so no protocol handler ever sees an unresolved
trailing-slash URL. Upstream behaviour: https://curl.se/docs/manpage.html,
`--upload-file`.

## Acceptance criteria

- [ ] `-T local.txt ftp://host/dir/` produces the URL `ftp://host/dir/local.txt`
      before the transfer starts.
- [ ] A URL not ending in `/` is left unchanged.
- [ ] The resolution lives in `Curl.Cli.UnitLibrary`; no protocol handler changes.

## Notes

## Log

- 2026-09-25: Migrated from Documentation/Planning/Backlog.md (Ready).
