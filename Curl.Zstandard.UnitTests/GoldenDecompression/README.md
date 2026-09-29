# Reference corpus frames

Copied unchanged, as ADR-0185 directs, from the Zstandard reference implementation,
github.com/facebook/zstd, at commit `01b7154f1172432f8abe9b3bb9909e14a1176b7d`:

| File here | Upstream path | Must |
| --- | --- | --- |
| `GoldenDecompression/block-128k.zst` | `tests/golden-decompression/block-128k.zst` | decode |
| `GoldenDecompression/empty-block.zst` | `tests/golden-decompression/empty-block.zst` | decode |
| `GoldenDecompression/rle-first-block.zst` | `tests/golden-decompression/rle-first-block.zst` | decode |
| `GoldenDecompression/zeroSeq_2B.zst` | `tests/golden-decompression/zeroSeq_2B.zst` | decode |
| `GoldenDecompressionErrors/truncated_huff_state.zst` | `tests/golden-decompression-errors/truncated_huff_state.zst` | fail |
| `GoldenDecompressionErrors/zeroSeq_extraneous.zst` | `tests/golden-decompression-errors/zeroSeq_extraneous.zst` | fail |

BL-859 copied the files that exercise frames, raw, RLE and literals-only compressed
blocks. `tests/golden-decompression-errors/off0.bin.zst` holds sequences and comes with
BL-860.

## Licence

The files are distributed under the zstd project's BSD licence:

```
BSD License

For Zstandard software

Copyright (c) Meta Platforms, Inc. and affiliates. All rights reserved.

Redistribution and use in source and binary forms, with or without modification,
are permitted provided that the following conditions are met:

 * Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.

 * Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

 * Neither the name Facebook, nor Meta, nor the names of its contributors may
   be used to endorse or promote products derived from this software without
   specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON
ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
```
