# ADR-0868 — Output files are opened shared for reading and writing

- Status: Accepted
- Date: 2026-10-28
- Task: BL-3703 (CGR-174)
- Decided by Claude under Stewart's delegation.

## Context

The third-party gap report (CGR-174) found that on Windows two `-Z` transfers writing the same
`-o` file fail one of them with exit 23 (`client returned ERROR on write of 5000 bytes`), where
curl 8.22.0 (Schannel) exits 0 for both. `PhysicalFileSystem.OpenForWriteAsync` opened every write
with `FileShare.Read`, so on Windows the second writer's open was a sharing violation. curl opens
an `-o` file with the C runtime's `fopen`, which on Windows shares it for reading and writing
(`_SH_DENYNO`), and off Windows with `open(2)`, which locks nothing.

## Decision

Every write open in `PhysicalFileSystem`, with or without a Unix create mode, shares the file for
reading and writing (`FileShare.ReadWrite`). A second writer opens the file, and each writes at its
own position, as two `fopen` streams do. Delete sharing is not added: `fopen` does not share for
delete either.

## Consequences

The repro command gives curl's exit 0, `-w` output, no stderr and the same 20000-byte file on
Windows. Off Windows nothing changes: .NET enforces only `FileShare.None` there. Another process
may now write an output file while Curl writes it, as it may while curl does.
