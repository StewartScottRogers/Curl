# Frames libzstd compressed

The upstream golden corpus (`GoldenDecompression/README.md`) holds no frame that decodes
with sequences, so these frames were compressed by libzstd 1.5.7 itself, through Windows'
bsdtar 3.8.8 (libarchive 3.8.8), for BL-860:

```
tar --format raw --zstd --options zstd:compression-level=<level> -cf <frame> <content>
```

Each frame has a `Content_Checksum`, no `Frame_Content_Size`, and the window libzstd picked
for its level. `ZstandardDecoderGoldenCorpusTests` pins the length and SHA-256 of the
content each was compressed from.

| Frame | Level | Content | Bytes | SHA-256 of the content |
| --- | ---: | --- | ---: | --- |
| `readme-level3.zst` | 3 | `GoldenDecompression/README.md` as BL-859 committed it | 2765 | `012d4024b8945fd43d47613cd45aadd7f52a7704b07dbc509a57acecdc03fbc6` |
| `source-level19.zst` | 19 | BL-859's `Curl.Zstandard.UnitLibrary/*.cs`, concatenated | 61181 | `9907bf43d276cc05bb9b8eb4cf22dfdaa2466e6aa868f6af0e76ec420b7d5e00` |
| `source-level-5.zst` | -5 | the same | 61181 | `9907bf43d276cc05bb9b8eb4cf22dfdaa2466e6aa868f6af0e76ec420b7d5e00` |
| `decisions-level3.zst` | 3 | the first 400000 bytes of `Documentation/Planning/Decisions/ADR-0*.md`, concatenated | 400000 | `9fed8db9b344171a577aa1201857b14cea58010f94a5b5885d2cd0db4a3743ab` |
| `binary-level9.zst` | 9 | the first 100000 bytes of the `dotnet.exe` host installed with the .NET 10 SDK | 100000 | `fbb59356c9bd0270705ae453f01ac0bad5ea910a495ccfa9d45af408a28abdc8` |

They cover text and machine code, levels -5 to 19, and one block to four (the 400000-byte
frame), so matches reach into earlier blocks.
