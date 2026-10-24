# Building from source

[← back to README](../README.md) · [Русская версия](building.ru.md)

PassGen is built with the compiler that ships with .NET Framework 4, which is already part of Windows. Visual Studio, the .NET SDK or any other tooling is **not needed**.

## Requirements

- Windows with .NET Framework 4.x (built into Windows 10 and 11; on Windows 7 install .NET Framework 4.x).
- `csc.exe` from `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\` — it supports **C# 5** (see [language limits](#language-limits)).
- `git`, if you want to clone the repository and publish changes.

## Build

```text
git clone https://github.com/rodioniurev/passgen.git
cd passgen
build.bat
```

The result is `PassGen.exe` in the repository root (about 125 KB).

What `build.bat` does:

1. If `PassGen.exe` already exists, it closes a running copy with `PassGen.exe /exit` — otherwise the file is locked.
2. Compiles every `src\*.cs` into `PassGen.new.exe`:
   - `/target:winexe` — a windowed application without a console;
   - `/optimize+` — optimizations on;
   - `/codepage:65001` — the sources are UTF-8;
   - `/win32icon` — the file icon in Explorer;
   - `/win32manifest:src\app.manifest` — crisp rendering at 125–200 % display scaling (`dpiAware`) and Windows 7–11 compatibility;
   - `/resource:src\passgen.ico,passgen.ico` — the same icon as a resource: the tray icon is drawn from it.
3. Swaps `PassGen.exe` for the new file. A running (or antivirus-locked) exe cannot be overwritten but can be renamed, so the old file is first renamed to `PassGen.old.exe` and then deleted.

If the build fails with "the file is being used by another process", close PassGen (`PassGen.exe /exit`, or *Exit* in the tray menu) and run it again.

## Language limits

The in-box compiler understands C# 5 only. So the code **cannot** use:

- string interpolation `$"…"`;
- the `?.` operator, expression-bodied members (`=>`), `nameof`, auto-property initializers;
- inline `out` variable declarations (`out var x`), tuples, local functions, `using static`;
- pattern matching (`is Type x`), `throw` expressions, exception filters (`when`).

This is a deliberate trade-off: in return you get a single ~125 KB exe that needs neither a runtime installer nor an SDK.

## Icon

The icon (`src\passgen.ico`) is drawn by a script and committed ready-made. To redraw it:

```text
powershell -NoProfile -ExecutionPolicy Bypass -File tools\make-icon.ps1
```

The script builds the `.ico` from 16–256 px frames: small ones as classic DIBs (which `System.Drawing.Icon` reads for the tray), large ones as PNG.

## Tests

Two scripts run against the built exe. They open no windows and press no keys:

```text
powershell -NoProfile -ExecutionPolicy Bypass -File tests\GeneratorTests.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tests\LocalizationTests.ps1
```

- `GeneratorTests.ps1` — the password generator: character sets, "Easy to type", blocks, minimum counts, edge cases, the clipboard-text cleanup.
- `LocalizationTests.ps1` — the translation table: no missing or empty translations, matching `{0}` placeholders, no typos in keys used by the sources, the tray tooltip length limit, plural forms, language selection.

Exit code `0` means everything passed. The UI, the keyboard hook, typing and focus handling are not covered by automated tests — use the [manual checklist](../CONTRIBUTING.md#manual-checklist).

## Continuous integration and releases

[`.github/workflows/build.yml`](../.github/workflows/build.yml) builds the exe on `windows-latest` for every push and pull request, runs both test scripts and uploads `PassGen.exe` as an artifact.

To make a release:

1. Bump the version in `src/AssemblyInfo.cs` (two lines: `AssemblyVersion` and `AssemblyFileVersion`) and add an entry to [CHANGELOG.md](../CHANGELOG.md).
2. Commit and tag:

   ```text
   git tag v1.3.0
   git push origin main --tags
   ```

3. For a `v*` tag the workflow builds the exe and creates a GitHub release with `PassGen.exe` and `PassGen.exe.sha256` (checksum) attached.

## Repository layout

```text
.github/            issue and pull request templates, the build workflow
docs/               documentation and screenshots (docs/img/<language>/)
src/                sources (C# 5), the manifest and the icon
tests/              automated tests (generator, localization)
tools/              helper scripts (icon drawing)
build.bat           build script
```
