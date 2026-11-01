# ADR-1178: Curl.slnx groups curl's own protocols under /Original/ and everything Curl adds under /Extended/

- Status: Accepted
- Date: 2026-10-31
- Amends ADR-0731
- Asked for by Stewart (2026-10-31: rework the solution folders for the renamed projects, option A); the
  word `Features` for the option extensions decided by Claude under Stewart's delegation (BL-4441)

## Context

After ADR-1176 and ADR-1177, `Curl.slnx` had four top-level folders for protocols and extensions:
`/Original/Protocols/`, `/Extended/Protocols/`, `/Protocols/Abstractions/` and `/Extensions/`, the last holding
the option extensions with the extension plumbing.

## Decision

The solution folders follow the two words in the project names:

- `/Original/Protocols/<Name>/`: curl's own protocols, `Curl.Original.Protocol.<Name>.*`.
- `/Extended/Protocols/<Name>/`: extensions that serve their own URL scheme, `Curl.Extended.Protocol.<Name>.*`.
- `/Extended/Features/<Name>/`: the option extensions, `Curl.Extension.<Name>.*`.
- `/Extended/Abstractions/` and `/Extended/Composition/`: `Curl.Extension.Abstractions.*` and the registry,
  `Curl.Extension.Composition.*`.
- `/Protocols/Abstractions/`: `Curl.Protocol.Abstractions.*`, shared by original and extended protocols, so it
  sits under neither.

`/Extensions/` is gone. Everything else in ADR-0731 stands.

## Consequences

- A new extension still joins with one single-line `<Folder>` directly under the marker comment (ADR-1003,
  `merge=union`), in `/Extended/Features/<Name>/` or `/Extended/Protocols/<Name>/`.
- Solution folders change only `Curl.slnx`; no build, test or script reads them. Folders on disk stay flat.
- Task files outside Backlog and Doing, ADRs before this one and audit and gap findings keep the old folder
  names as history.
