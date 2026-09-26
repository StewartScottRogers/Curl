# Curl.Protocol.File.UnitLibrary

Phase 1.

Local file access. No network involved.

**URL schemes:** `file`

This library may reference `Curl.Protocol.Abstractions.UnitLibrary` and nothing
else horizontal. Referencing another protocol library is a build break, and
`Curl.Protocol.Abstractions.UnitTests` fails if one appears.

Never construct a `FileStream` here. Take `IFileSystem` so the tests in the
matching `.UnitTests` project can drive this code against an in-memory fake with no
disk access. See `Documentation/Planning/Decisions/ADR-0002-ifilesystem-as-the-second-protocol-seam.md`
for why `file` uses `IFileSystem` instead of the `IConnection` seam every other
protocol library uses.
