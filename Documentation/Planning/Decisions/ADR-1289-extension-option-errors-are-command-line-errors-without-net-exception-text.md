# ADR-1289: Extension option errors are command-line errors without .NET exception text (BL-5344)

Decided by Claude under Stewart's delegation.

## Status

Accepted

## Context

`--ohttp-keys` without `--ohttp-relay` printed `curl: (2) ... (Parameter 'values')`, while `--npipe` and the other hooks that refuse their options at activation printed `curl: <message>` plus the try-help line. A `-K` file holding an expander-only option such as `--http-file` printed a second line, `option -K: cannot be used in a config file`, blaming `-K`.

## Decision

An extension option that is refused when its hooks are activated is a command-line error: `curl: <message>`, then curl's `try 'curl --help'` line, exit 2, as for `--npipe`. This now covers transfer hooks too (`ExtensionTransferHooksOf` catches `ArgumentException` as the other hook kinds do), and the message never carries the exception's `(Parameter '...')` text. Messages a hook produces while a transfer runs (`--checksum`, `--segments`) stay `curl: (2) ...`, as they report a transfer.

A config file refusal of an expander-only option prints the file line and the try-help line only; the file line already names the cause.

`--extended-help` shows `--log-level <level>, --log-file <file>` as `--ai-help extensions` does.
