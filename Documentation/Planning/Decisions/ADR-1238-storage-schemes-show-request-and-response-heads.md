# ADR-1238: The s3, gs, az and oci schemes show request and response heads under -v, -i and -D

Status: Accepted. Decided by Claude under Stewart's delegation (BL-4916).

## Context
The four HTTP-based storage schemes wrote their own HTTP/1.1 exchange and reported nothing to the verbose or header output, so `-v` showed no `>`/`<` lines and `-i`/`-D` printed only the body (finding EXT-005).

## Decision
Each library has a small `<Scheme>HttpHeadReporter`: the request head goes to `ITransferEvents.ReportRequestHeader` (`-v` prints `>` lines) and every response head line, the status line and the final empty line included, goes to `ITransferEvents.ReportResponseHeader` (`<` lines) and to `ITransferContext.HeaderOutput` (`-i`, `-D`), as the http handler does. Connections built without a reporter use `None` and show nothing.

## Consequences
The reporter is duplicated per library because extensions reference only the abstractions and never each other.
