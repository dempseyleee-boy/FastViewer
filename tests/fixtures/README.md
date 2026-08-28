# FastViewer fixture files

These files are tiny 16x16 raw RGB word dumps used by `dist/FastViewer.exe --self-test`.

They cover the `RGB_RnGnBn_48B` family where each RGB channel is stored in one 16-bit little-endian word and `_MSB` / `_LSB` controls bit alignment inside that word. The pixel values are deterministic gradients generated from `(x, y)` so the self-test can verify decoded pixels instead of only checking file size.