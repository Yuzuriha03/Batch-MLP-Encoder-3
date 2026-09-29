# Changelog

## 4.0 — 2026-09-29

This release is based on **Batch MLP Encoder 3.0.6**. Besides the move to .NET 10,
it fixes several long-standing defects in the bit-depth handling that made parts of
the "Bit depth" page unusable.

### Platform

- Migrated from **.NET Framework 4.6.2** to **.NET 10** (`net10.0-windows`, WinForms).
  No NuGet packages are required; everything used comes from the
  `Microsoft.WindowsDesktop.App` framework.
- Still a native **x86** application, because `MediaInfo.dll` is a 32-bit native
  library and Surcode MLP Encoder is a 32-bit program.
- The `.NET Framework 4.6 or later` registry check at startup is gone — the runtime
  handles that before the process starts.
- High DPI is now configured with `Application.SetHighDpiMode(SystemAware)` instead
  of the old `app.manifest` + `EnableWindowsFormsHighDpiAutoResizing` combination.
  The process DPI awareness level is unchanged (`SYSTEM_DPI_AWARE`).
- `Encoding.Default` changed meaning between the two runtimes: it was the system ANSI
  code page on .NET Framework (CP936 on a Simplified Chinese system) but is **UTF-8**
  on .NET 10. A new `LegacyTextEncoding` class always uses the real ANSI code page
  (`GetACP`), so the generated `.ssf` files and the eac3to log reading are
  byte-for-byte identical to 3.0.6, and the Hangul workaround below still triggers
  for exactly the same file names.

### Fixed — the bit-depth options did not work in 3.0.6

In 3.0.6 the whole "Bit depth" group was either disabled or produced broken
eac3to command lines:

| Problem in 3.0.6 | What happened | Fixed in 4.0 |
|---|---|---|
| `Page3AlwaysRebitRadioButton` had `Enabled=False` in `MainForm.resx`, and its label ended with "(Unavailable)" | "Always convert to the chosen bit depth" could not be selected at all | The option is enabled and the "(Unavailable)" suffix is gone |
| `Page3OnlyRebitRadioButton` compared the source file's **own** bit depth against `"16"/"20"/"24"` and emitted `-down16` / `-down20` / `-down24` | It asked eac3to to convert to the bit depth the file **already had**. eac3to detects the match and skips, so the option was a silent no-op | A new `DecideRebit()` compares the requested depth against the depth eac3to will actually output, and only emits `-downN` when the depth has to be **lowered** |
| The same option's `else` branch (bit depth is not 16/20/24, e.g. 32-bit or unknown) emitted `-down` + **the chosen sampling rate**, producing e.g. `-down48000` | eac3to reports an unknown parameter and exits with code 1, so the **entire file failed to convert** | The sampling rate is never used as a bit-depth argument |
| The list of "allowed" sampling rates contained `"176000"` | A 176.4 kHz source was wrongly treated as needing resampling in "Only convert" mode | Corrected to `"176400"` |
| Nothing at all was done when the target bit depth was **higher** than the source | eac3to cannot raise bit depth, so 16 → 24 bit silently produced 16-bit output | New lossless upconversion, see below |

The priority of the "Always 16 bits" checkbox (it is a `CheckBox` despite the name,
and it is evaluated before the other bit-depth options) is intentionally preserved
from 3.0.6.

### New — lossless bit-depth upconversion

- eac3to's `-downN` can only **lower** bit depth. Verified exhaustively against
  eac3to 3.66: for a 16-bit source, `-down24`, `-down20`, `-resampleTo<same rate>`,
  `+0dB` and `-16` all keep it at 16 bit, and `-up24` / `-bit24` / `-depth24` /
  `-dither24` are rejected as unknown parameters.
- 4.0 therefore rewrites the WAV itself: each sample is shifted left and zero-padded
  (`s24 = s16 << 8`). This is pure integer bit manipulation with no floating point,
  so it is mathematically lossless — verified over 10,354,378 samples with zero
  differing bytes.
- The upconversion runs **after** eac3to has split the channels, not before. If a
  source is padded to 24 bit first, eac3to notices that the extra bytes are all zero
  (`"Superfluous zero bytes detected, will be stripped"`) and strips them back to
  16 bit, silently undoing the work.
- The rewritten WAV uses a **plain PCM** header (format tag `1`, 16-byte `fmt` chunk).
  Surcode MLP Encoder is from 2003 and rejects `WAVE_FORMAT_EXTENSIBLE`
  (tag `0xFFFE`, 40-byte `fmt`) with `Invalid Wave File`. For the same reason,
  building the file with ffmpeg is not an option — ffmpeg always writes
  `WAVE_FORMAT_EXTENSIBLE` for 24-bit PCM. 4.0 does not use ffmpeg at all.
- Downconversion is explicitly refused by the upconverter rather than silently
  truncating samples; that is still eac3to's job via `-downN`.

### New — file names Surcode cannot handle

3.0.6 wrote paths into the `.ssf` file using the system ANSI code page. Any character
that code page cannot represent (Korean, for instance) became `?`, and Surcode then
reported that it could not find the file.

- File names that need it are now replaced by a temporary pure-ASCII name
  (`__surcode_0001`) for the whole eac3to → `.ssf` → Surcode pipeline.
- After **all** files have been encoded, the generated `.mlp` files are renamed back
  to their original names. Doing it once at the end, rather than after each file, is
  far more reliable: Surcode has already exited and released its file handles.
- If the temporary or output folder itself contains such characters, the folder
  cannot be renamed safely, so a warning is shown before encoding starts.
- A rename failure does **not** mark the file as failed — the audio is correct, only
  the name could not be restored. The count of such files is reported separately so
  that a partial rename is never reported as a complete failure, and a fully
  successful run is never reported as clean when a name is still wrong.

### New — resilience against files that are still in use

- Surcode, eac3to, the Windows indexer and antivirus tools can hold file handles
  briefly after a file is written, so a single `File.Move` / `File.Delete` could fail
  with *"The process cannot access the file because it is being used by another
  process"*.
- Deletions and renames now retry for a bounded time (250 ms apart, 3 s for stale
  temp files, 5 s for the final rename) instead of failing on the first attempt.
- The waiting message reports real progress rather than resetting the progress bar.
- The final rename deliberately ignores cancellation: interrupting it would leave the
  output permanently named `__surcode_0001`.

### Other behaviour changes

- "Only convert audio files with not allowed bit depths" now warns (once per batch)
  that a 20-bit target is stored in a 24-bit container, since no 20-bit WAV container
  exists, so the resulting MLP may end up 24-bit.
- When the requested bit depth is higher than the source, the log now explains that
  the channels will be expanded losslessly after eac3to splits the file, instead of
  failing silently.
- When a bit depth cannot be raised, the user is told that the file keeps its
  original depth rather than being given a misleading success message.

### User interface

- **About page — word wrap.** `Page1AboutTextbox` had `WordWrap=False` and
  `ScrollBars=Both`, so the long licence paragraphs could only be read by scrolling
  horizontally. Word wrap is now on and the horizontal scrollbar is gone.
- **About page — content and layout.** Rewritten in all three languages:
  title and version first, then author, links, and four sections
  (Runtime / How it works / Translators / Licence), each preceded by a blank line
  with an indented body. Author is now **Yuzuriha03**, with the original author
  credited ("Based on Batch MLP Encoder 3 by Sad Pencil"). Copyright is
  **2016–2026**.
- **About page — no more mixed languages.** In 3.0.6 the Chinese and Spanish About
  pages still contained the English GPL notice. Each language is now fully in its
  own language, and the long GPL paragraphs were replaced by a single licence name
  and link.
- **Version.** The title bar and About page show `4.0`;
  `AssemblyVersion` / `AssemblyFileVersion` are `4.0.0.0`. In 3.0.6 the assembly
  version used the wildcard `3.0.6.*`, which changed on every build and — because
  `user.config` is stored per version — silently reset the user's saved paths
  every time.

### Build and distribution

- Project converted to an SDK-style `.csproj`; the 3.0.6 project file is kept as
  `Batch-MLP-Encoder-3.csproj.net462.bak`.
- `LICENSE` (GPL v2 full text) is now copied into the build and publish output.
  The 3.0.6 release zip contained only the executable, its config, `MediaInfo.dll`
  and the two satellite assemblies — no licence text at all.
- **Self-contained x86** package (`Batch-MLP-Encoder-4.0.zip`) is provided, so
  nothing has to be installed on the target machine.
- Builds cleanly with 0 warnings and 0 errors.

### 3.0.6 → 4.0 at a glance

| Area | 3.0.6 | 4.0 |
|---|---|---|
| Runtime | .NET Framework 4.6.2 | .NET 10 (x86) |
| Startup check | Reads `NET Framework Setup\NDP\v4\Full` in the registry | None needed |
| High DPI | `app.manifest` + `app.config` | `Application.SetHighDpiMode(SystemAware)` |
| ANSI-encoded `.ssf` | `Encoding.Default` (= ANSI on Framework) | `LegacyTextEncoding` (= ANSI via `GetACP`) |
| "Always convert to chosen bit depth" | Disabled, labelled "(Unavailable)" | Works |
| "Only convert disallowed bit depths" | Silent no-op; failed outright for 32-bit/unknown sources | Works correctly |
| Bit depth 16 → 24 | Not possible (eac3to cannot raise it) | Lossless in-place WAV rewrite |
| 176.4 kHz detection | Typo `176000` | `176400` |
| Korean / non-ANSI file names | Failed: `?` written into `.ssf` | Temporary ASCII name, renamed back at the end |
| File in use | Failed on first attempt | Retries, then reports clearly |
| About page word wrap | Off (horizontal scrolling) | On |
| About page language | Chinese/Spanish pages included the English GPL text | Fully localised, licence shown as a link |
| Assembly version | `3.0.6.*` (changed every build) | `4.0.0.0` (fixed) |
| Licence text shipped | Not included | `LICENSE` in the output |
| Distribution | Requires .NET Framework 4.6 | Self-contained x86 zip available |
