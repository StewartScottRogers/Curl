# ADR-1108: Proto imports resolve through an injected reader, and types resolve as protoc does

- Status: Accepted
- Date: 2026-10-29
- Task: BL-4168 (split from BL-4165)

Decided by Claude under Stewart's delegation.

## Context

`Curl.Protobuf.UnitLibrary` reads proto3 text into descriptors (BL-4170, `ProtoParser.TryParse`). The gRPC,
gRPC-Web and Connect extensions also need a file's services and `rpc` methods, the files it imports, and
which message or enum each named type means. Upstream curl has no Protocol Buffers support, so the
reference is protoc and the language specification at https://protobuf.dev/reference/protobuf/proto3-spec/.

## Decision

- **`ProtoParser.TryParse` stays a single-file read**: it now also reads `service`, `rpc` (with `stream` on
  either side) and `import` (default, `public`, `weak`) statements, but resolves nothing, so a file can be
  read without its imports.
- **`ProtoFileSetReader.TryRead(path, IProtoFileReader, ...)`** reads a file and its imports depth first,
  each path once, through the injected `IProtoFileReader`; the library never touches the file system. The
  reader is synchronous (`TryReadFile`): parsing is CPU work, and a host reading from disk or memory has no
  asynchronous source to wait on. Paths are passed to the reader as written; how a path maps to a file
  (protoc's `-I` search) is the host's.
- **Errors are one typed `ProtoFileError`** (path, `ProtoFileErrorKind`, line, column, message). A missing
  import and an import cycle are reported at the `import` keyword in the importing file, as protoc reports
  them; a file importing itself is a cycle. A malformed imported file reports its own path and position.
- **Name resolution follows protoc**: every message and enum gets a fully qualified name (`.pkg.Outer.Inner`);
  a relative name is tried in the innermost scope first and then each enclosing scope out to the root, and
  a leading `.` names the type absolutely. A file sees its own types, those of the files it imports, and
  those of files imported `public` by those, at any depth; `weak` is read like a default import. A type
  that does not resolve is `UnresolvedType`; a method whose request or response is an enum is `NotAMessage`
  and a scalar there is a syntax error.
- **Resolutions are looked up, not written into the descriptors**: `ProtoFileSet.FindType(reference)` returns
  the type a reference read in the set resolved to, keyed by the reference object, and `Types` holds every
  type by full name. The descriptors stay "as written", and BL-4170's records are unchanged in shape apart
  from the new `Imports` and `Services`.

## Consequences

- protoc resolves a dotted name by its first component and then requires the rest inside it; this reader
  tries the whole dotted name at each scope, which differs only for names that shadow an outer package.
- A type defined twice across the set keeps the first definition; protoc rejects the duplicate.
- Duplicate method names in a service are not rejected.
