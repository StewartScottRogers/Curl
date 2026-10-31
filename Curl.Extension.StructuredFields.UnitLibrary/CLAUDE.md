# Curl.Extension.StructuredFields.UnitLibrary

The Structured Field Values extension (ADR-0999, ADR-1152), tested against RFC 9651. Namespace
`Curl.Extension.StructuredFields`. It references only `Curl.Extension.Abstractions.UnitLibrary` and the shared
layers it brings.

- `StructuredFieldsExtension` gives the `--structured-fields` flag, its description and its hook activator, which
  `Curl.Extension.Composition` registers.
- `StructuredFieldsTransferHook` parses the response's known Structured Fields after the transfer, reports each as
  the `-v` line `* Structured field <name>: <readable>`, and gives `-w` `%{structured_fields}` and
  `%{structured_field.<lowercase name>}`. It sends nothing and changes no output.
- `KnownStructuredFields` names the known response fields and their top-level types (`StructuredFieldType`) and
  gives a field's readable line; `StructuredFieldParser` is RFC 9651 §4.2 into the `StructuredValues` records;
  `ReadableStructuredValue` writes them as one line that names every type.

Tested against the specification, not against curl. Held to the four quality gates in the root
`CLAUDE.md`. Its tests are in `Curl.Extension.StructuredFields.UnitTests`.
