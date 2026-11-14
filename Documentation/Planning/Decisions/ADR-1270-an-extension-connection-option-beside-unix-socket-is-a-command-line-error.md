# ADR-1270: An extension connection option beside --unix-socket is a command-line error with exit 2

- Status: Accepted
- Date: 2026-11-13
- Decided by Claude under Stewart's delegation (BL-5133)

## Context

ADR-1014 refuses two extension connection-hook options with exit 2, because each supplies the connection a
transfer runs over. curl's own `--unix-socket` and `--abstract-unix-socket` supply it too, but the check did not
see them: `--npipe` with `--unix-socket` ran over the pipe and ignored the socket, and `--vm-socket` with
`--unix-socket` tried the vsock (finding EXT-056). Upstream curl has no extension options, so there is no curl
output to measure.

## Decision

- Any activated extension connection hook beside `--unix-socket` or `--abstract-unix-socket` is the same
  command-line error as ADR-1014's: `curl: the options --npipe and --unix-socket each supply the connection a
  transfer runs over; give only one of them`, the try-help line, exit 2, before the group connects to anything.
  The message names the curl option as given (`--abstract-unix-socket` when that one set the socket).
- The rule covers every connection hook, not only `--npipe` and `--vm-socket`: each one replaces the connector
  curl would use for the socket, so ignoring the socket would be the same silent drop.
- `Curl.Extension.Composition`'s `CurlSocketConnectionConflict.RefuseBesideCurlSocket` holds the check, given
  the curl option's name, so no extension or Composition references the Cli options;
  `CurlComposition.ExtensionConnectionHooksOf` calls it after `ConnectionHooks.ActivatedBy`.
- `--extended-help` (and so `--ai-help`) says `--npipe` and `--vm-socket` cannot be combined with
  `--unix-socket` or `--abstract-unix-socket`.

## Consequences

A command line that relied on the extension option winning over `--unix-socket` now fails with exit 2 instead of
silently ignoring one of them.
