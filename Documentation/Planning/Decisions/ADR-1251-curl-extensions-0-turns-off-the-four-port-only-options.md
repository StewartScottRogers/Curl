# ADR-1251 — CURL_EXTENSIONS=0 turns off --ai-help, --extended-help, --log-level and --log-file

- Status: Accepted
- Date: 2026-11-11
- Task: BL-4937
- Decided by Claude under Stewart's delegation.

## Context

Setting `CURL_EXTENSIONS=0` is meant to make the port plain curl. Four options only Curl has sat in the core
option table and still worked: `--ai-help`, `--extended-help`, `--log-level` and `--log-file`, and `--help` still
named `--ai-help` (outside review finding EXT-015).

## Decision

- Under `CURL_EXTENSIONS=0` the parser does not find those four long names (`CommandLineOptionTable.PortOnlyLongNames`,
  `CommandLineParser.TryFindLongOption`), also through `--no-` and `--expand-` spellings, so each ends as curl ends an
  unknown option: `curl: option --log-file: is unknown`, the try line, exit 2, and no log file is created.
- `--help` (usage, `all`, `curl`, any category) then leaves out the `--ai-help` usage line and table row, `--help ai-help`
  is the unknown-category page and `--help --ai-help` is the incorrect-option-name message, so the pages are curl 8.22.0's bytes.
- No exception is kept: nothing stays on under `0`. With extensions on, `--extended-help` and `--ai-help extensions`
  also list `--log-level` and `--log-file` as a `diagnostic-log` entry, because they belong to no extension.
- The Extended conformance cases Listing-2 and Listing-4 (which pinned "extensions are off" text) now pin the unknown-option refusal.