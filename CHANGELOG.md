# Changelog

All notable changes to this project are documented in this file. The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow [Semantic Versioning](https://semver.org/).

Versions up to 1.2.0 were developed before the project was published; the git history starts at 1.3.0.

## [1.3.0] — 2026-10-24

### Added
- **Multilingual interface: English, Spanish and Russian.** The language follows the Windows display language. It can be changed at any time from the tray menu (*Language*) without restarting, and is stored as `lang=` in `PassGen.ini` (`auto`, `en`, `es` or `ru`).
- `README.md`, `README.es.md` and `README.ru.md` with screenshots of every language and the shortcut reference.
- Automated checks of the translations (`tests/LocalizationTests.ps1`), run by the CI workflow.
- English documentation: building, architecture, contributing guide and security policy. The Russian documents are kept as `docs/*.ru.md`.

### Changed
- All interface text moved out of the forms into one translation table, `src/Loc.cs`.
- Author, license and repository details filled in (`LICENSE`, assembly information, documentation).

## [1.2.0] — 2026-10-23

### Added
- Keyboard shortcut help: *Keyboard shortcuts* in the tray menu, `F1` in the generator window and a link under the main button.
- MIT license, documentation, automated generator tests, a build and release workflow for GitHub Actions.

### Changed
- The sources are split into files by class; `build.bat` compiles all `src\*.cs`.
- The icon drawing script moved to `tools\make-icon.ps1`.

## [1.1.0] — 2026-10-23

### Added
- **Quick typing without a window** (global keyboard hook):
  - `Right Alt + P` — a new password: copied to the clipboard, appended to the file and typed character by character where the caret is. The entry's description is the active window's title (the `autodesc` setting);
  - `Right Alt + Shift + P` — type the last password again (a "repeat password" field);
  - `Right Alt + V` — type the clipboard contents character by character (limit: 500 characters).
- A quick typing switch in the tray menu; the `quick`, `autodesc` and `typedelay` settings in `PassGen.ini`.
- Support for layouts with AltGr (Spanish, Russian "typewriter"): the right Alt is told apart from `Ctrl + Alt`.

### Fixed
- After the generator window closes (including when a click on the tray icon closed it), focus returns to the program the user was working in.
- The generator window gets focus more reliably and puts the caret into the description box.
- Typing is faster: the pause between characters is now really 8 ms.

## [1.0.0] — 2026-10-22

First version.

### Added
- A password generator in the tray; the window opens on a click on the icon and on `Ctrl + left Alt + P`.
- Settings: length 4–128, character sets, a custom list of special characters and their minimum count, the "Easy to type" mode, hyphen-separated blocks, "start with a letter", a strength estimate.
- Saving to a text file: `date - password - description`; choosing the file; the password stays out of the Windows clipboard history.
- Autostart, a single instance, the tray icon, settings stored in `PassGen.ini`.
- Command line: `PassGen.exe /autostart` (no window) and `PassGen.exe /exit` (cleanly closes a running copy).
