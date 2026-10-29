---
id: BL-4060
title: Drop copied post fields on CURLOPT_POSTFIELDSIZE only when the size grows
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Libcurl/Options/Http/EasyHttpOptionSetters.cs, Libcurl.UnitTests]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4060 — Drop copied post fields on CURLOPT_POSTFIELDSIZE only when the size grows

## Goal

`CURLOPT_POSTFIELDSIZE` clears data set with `CURLOPT_COPYPOSTFIELDS` only when the new size is larger than the old one, as curl 8.22.0's `lib/setopt.c` does.

## Context

Found by the shift-end review of 2026-10-28 (code-reviewer, ADR-0710), in the diff `354cb7711...c4b167258`.

- `Libcurl/Options/Http/EasyHttpOptionSetters.cs:85`: `SetPostFieldSize` sets `PostFields = h.PostFieldsAreCopy ? null : h.PostFields` and clears `PostFieldsAreCopy` on every call.
- libcurl frees the copied data only under `if(s->postfieldsize < arg && data->set.postfields == data->set.str[STRING_COPYPOSTFIELDS])`, that is, when the size grows.
- Example: `POSTFIELDSIZE 10`, then `COPYPOSTFIELDS "abcdefghij"`, then `POSTFIELDSIZE 5` (or 10 again). libcurl posts the data; Libcurl now posts nothing.

## Acceptance criteria

- [x] `SetPostFieldSize` drops copied fields, and clears `PostFieldsAreCopy`, only when the new size is larger than `PostFieldSize`. It keeps them, still marked as a copy, otherwise.
- [x] Tests in `Libcurl.UnitTests` cover a smaller size, the same size and a larger size after `COPYPOSTFIELDS`. They check that the fields are kept, kept and dropped.
- [x] `dotnet build` is clean, the fast tests pass, and `Libcurl` still meets the four quality gates.

## Notes

Quality run for Libcurl: no Libcurl member fails; the one failing member in the report is in Curl.Engine.UnitLibrary, not touched here. CI criterion met by platform-neutral tests.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: e87f17ca b19de3b5 bbe0e011.
- 2026-10-29: Doing -> Done. SetPostFieldSize drops copied fields only when the size grows; tests added
