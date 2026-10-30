# ADR-1115 — Extensions add -w write-out variables through the transfer report, never shadowing curl's

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4327.

## Context

An extension (ADR-0999) such as the Structured Field Values one (BL-4140) must show what it learned in `-w`.
`TransferWriteOutVariables` resolves every `%{name}` from the `TransferResult` the transfer returned, and a
transfer hook's `ITransferHook.ExecuteAsync` already returns that result, so the result's `TransferReport` is
the one channel from a hook to the write-out that needs no new interface. Curl must stay a drop-in
replacement: with no extension active every `-w` output stays byte-for-byte what it was, and no extension may
change what one of curl's own variables prints.

## Decision

- **Where the values live.** `TransferReport.ExtensionWriteOutValues`, an `IReadOnlyDictionary<string, string>`
  from variable name to the text `%{name}` prints, empty by default. A hook returns
  `result with { Report = result.Report with { ExtensionWriteOutValues = ... } }`.
- **Curl's names win.** `TransferWriteOutVariables.TryGetVariableText` consults the dictionary only after every
  curl variable (`json`, `header_json` and every formatted one) has missed, and the renderer's own names
  (`stdout`, `stderr`, `onerror`, `header{...}`, `time{...}`) never reach it. So an extension can add a variable
  but never shadow one; a name given no value is still reported unknown with curl's warning.
- **Text only.** A value is printed as given; it is not added to `%{json}`, whose members are curl's own, so
  `%{json}` stays identical to curl's. Name matching is exact (case-sensitive), as curl's variable names are.
- **Naming.** An extension should prefix its names with its own (`sf_...`) so two extensions do not collide;
  the report holds one dictionary, so when two hooks set it the outermost hook's return decides.

## Consequences

No interface changes and nothing for a host to wire: a hook that sets no values leaves every output unchanged.
A curl release that adds a variable an extension already used takes the name over, which is the drop-in rule.
