# ADR-1417: Git alone owns line endings; .editorconfig names none

- Status: Accepted
- Date: 2026-11-26
- Decided by Claude under Stewart's delegation (BL-5923; Stewart, 2026-11-26: "do both")

## Context

`.editorconfig` said `end_of_line = crlf` for every file. `.gitattributes` says `* text=auto`, so
git stores LF and checks files out CRLF on Windows and LF on Linux and macOS. Those two rules
disagree. `dotnet format --verify-no-changes` reported 343 ENDOFLINE fixes on Windows, and on
Linux and macOS it would flag every file. "Fixing" those endings only churns working copies,
because git normalizes them again on the next commit.

## Decision

`.editorconfig` names no `end_of_line`. Git's `* text=auto` decides line endings everywhere.
`dotnet format` checks only what it can change for good: `using` order, whitespace and final newlines.

## Consequences

- `dotnet format --verify-no-changes` gives the same answer on Windows, Linux and macOS.
- Editors keep each file's existing line endings. Git normalizes anything new on commit.
- BL-5923's reformat shrinks from 681 files to 680 (no ENDOFLINE fixes) and is split by
  project group, so lanes can run the parts in parallel.
