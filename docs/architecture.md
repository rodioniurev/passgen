# How PassGen works

[← back to README](../README.md) · [Русская версия](architecture.ru.md)

This document is for people who want to understand or change the code. It describes how the program is organized and — separately — the non-obvious things learned the hard way. (Code comments are in Russian; the UI strings live in [`src/Loc.cs`](../src/Loc.cs).)

## Big picture

PassGen is a single WinForms process (.NET Framework 4, C# 5). Threads:

| Thread | What it does |
|---|---|
| Main (STA) | Tray icon, the generator window, help, the toast. Everything that touches the UI and the clipboard. |
| Hook thread | The global keyboard hook (`WH_KEYBOARD_LL`) with its own message loop. Needed only for `Right Alt + P/V`. |
| Wake thread | Waits for the named events "launched again" and "quit". |
| Typing workers | One short-lived thread per quick-typing request that injects the characters. |

```text
 keyboard ──► KeyHook (hook thread) ──► BeginInvoke ──► TrayApp.QuickType / QuickPaste (main thread)
                                                              │ generate, save to the file, copy
                                                              ▼
                                                        TypeAsync (worker thread)
                                                              │ waits for Alt/Shift release, types via SendInput
                                                              ▼
                                                        Toast (main thread)
```

## Source files

| File | Contents |
|---|---|
| `Program.cs` | `Main`: parses `/exit` and `/autostart`, the named events, starts `TrayApp`. |
| `AssemblyInfo.cs` | Title, version, copyright. |
| `Native.cs` | All WinAPI declarations (P/Invoke) and the `INPUT` / `MSG` structures. |
| `Loc.cs` | Localization: the translation table (English, Spanish, Russian), language detection, plural forms. |
| `Settings.cs` | Settings and reading/writing `PassGen.ini`. |
| `Gen.cs` | The password generator and the strength estimate. |
| `Platform.cs` | `Storage` (appending to the file), `Clip` (clipboard), `Autostart` (registry). |
| `QuickInput.cs` | `Win` (small window helpers), `Typer` (character injection), `KeyHook` (key interception). |
| `FocusKeeper.cs` | Remembers where to return focus, and hands focus to windows reliably. |
| `PopupForm.cs` | The generator window. |
| `HelpForm.cs` | The keyboard shortcuts window. |
| `Toast.cs` | The "Copied" toast that never takes focus. |
| `TrayApp.cs` | The tray icon and menu; ties everything together; the quick-typing scenarios. |

## Start-up and lifetime

- `Program.Main` creates the named event `PassGen.Wake`. If it already existed, the program is running: the second copy signals the first (which opens its window) and exits. That is how "one copy per session" works.
- `PassGen.Quit` is the second event: `PassGen.exe /exit` sets it and the running copy exits cleanly (`TrayApp.Quit` stops the hooks and removes the tray icon so no "ghost" icon stays behind).
- `/autostart` starts without a window (this is how autostart launches it).
- `TrayApp` is an `ApplicationContext` with no main form. Order in its constructor: settings → language → the generator window (with handles pre-created so it opens instantly) → `FocusKeeper` → the key hook → the menu and the icon → the event wake thread.

## Localization (`Loc.cs`)

- All UI text is in one table `Loc.Table`: a row per key, a column per language (`key | en | es | ru`). Adding a language means adding a code to `Loc.Codes`, a name to `Loc.Names` and one column.
- `Loc.Init(setting)` takes `auto` or a language code. `auto` (and any unknown value) resolves through `Loc.Detect()` — the two-letter code of `CultureInfo.CurrentUICulture` (the Windows **display** language) if supported, otherwise English.
- `Loc.T(key, args…)` returns the string for the current language, falls back to English and finally to the key itself (so a typo is visible, not fatal). `Loc.Plural(n, key)` picks the right word form (`character|characters`, `знак|знака|знаков`).
- Forms build all their text in their constructors, so **a language change re-creates the generator window** (`TrayApp.SetLanguage`): the old window is disposed (which releases the `Ctrl + Alt + P` registration), a new one is created and registers it again; the help window is closed; the menu texts and the tray tooltip are refreshed. No restart is needed.
- Layout copes with different text lengths: checkboxes use their preferred sizes, the minimum/exactly label is sized for the longer of its two texts, the help window sizes its key column to the longest key name.
- `tests/LocalizationTests.ps1` guards the table: complete, consistent placeholders, no typos in keys, tray tooltip ≤ 63 characters in every language.

## The generator (`Gen.cs`)

1. Alphabets: lower case, upper case, digits and special characters (the user's list, cleaned of letters, digits, spaces and duplicates). In "Easy to type" mode `0 O 1 l I |` are removed from the alphabets and "hard" special characters (`| \ / ` ' " ~ ^ < > ( ) [ ] { } ; : . ,`) from the symbol list. If the cleaned symbol list is empty, a fallback is used: `!@#$%^&*-_=+?` (in easy mode `!@#$%&*-_+=?`). In blocks mode `-` is removed from the symbols — it is the separator.
2. Mandatory characters: one from each enabled set plus `MinSymbols` special characters. If the password is too short for all of them, the extra special characters are dropped.
3. The remaining positions are filled from the common pool. In "Easy to type" mode special characters are not in that pool, so there are exactly `MinSymbols` of them.
4. The characters are shuffled (Fisher–Yates). "Start with a letter": if the first character is not a letter, it is swapped with a random letter.
5. Blocks: block sizes are chosen so the total length with hyphens is exactly the requested one and the average block is closest to 4.5 characters (`BlockSizes`).
6. Random numbers come from `RandomNumberGenerator`; a number in `[0, n)` is obtained by rejection sampling, so there is no modulo bias.
7. Strength estimate: `number of random characters × log2(pool size)`. In "Easy to type" mode it is more precise: characters from the symbol-free alphabet plus the choice of positions and values of the special characters (`Log2Choose`).

`Gen.Make(Settings, out double bits)` does not depend on the UI — the [automated tests](../tests/GeneratorTests.ps1) are built on it.

## Settings (`Settings.cs`)

A plain INI (`key=value`), read once at start-up and written on every change in the window. Values are clamped to their valid ranges when read. The password file path defaults to next to the exe.

## The generator window (`PopupForm.cs`)

- Created once, then only shown and hidden. The window and all its controls get their handles up front (`Prepare`), so showing it takes tens of milliseconds.
- Borderless (the 1 px border is the form's padding), shadow via `CS_DROPSHADOW`. Sizes are computed through `S()` from the system DPI; the manifest says `dpiAware = true`, otherwise Windows would blur the window on scaled displays.
- Any change of a setting calls `Changed()`: read the controls → save the INI → a new password. The `loading` flag keeps events from recursing when values are set programmatically.
- The window hides when it loses activation (`OnDeactivate` → `Dismiss`). Clicking the tray icon also takes focus from it, so `TrayApp.TrayClick` uses time windows (500 and 300 ms) to tell "open" from "close".
- `Ctrl + Alt + P` is registered with a plain `RegisterHotKey` on the window's handle.

### Focus (`FocusKeeper.cs`)

The problem: after a click on the tray icon focus goes to the taskbar, and when the PassGen window closes Windows does not always give it back to the program you were in — the caret "disappears".

The solution:

- `FocusKeeper` subscribes to `EVENT_SYSTEM_FOREGROUND` (`SetWinEventHook`, no DLL injection, events from our own process skipped) and remembers the last "real" window. Shell windows (the taskbar, menus, task switcher, …) are not remembered.
- The window records `returnTo` when shown. On `Enter`, `Esc`, a repeated shortcut and a repeated tray click (`DismissAndReturn`) focus goes back to that window. A plain hide on lost activation (`Dismiss`) leaves focus alone — the user clicked elsewhere on purpose.
- If Windows did not hand focus to the window, the caret can end up on the form instead of the description box: `focusTimer` puts the caret into the box a few times with a delay.
- Forced focus transfer through `AttachThreadInput` is used **only to return** focus. It must not be used to show our own window: attaching to the other window's thread makes it deactivate, and the window then hides itself.

## Quick typing

### Key interception (`KeyHook`)

A global `WH_KEYBOARD_LL` hook is needed: `RegisterHotKey` can't tell the left Alt from the right one and can't swallow a letter so that it never reaches the application. The hook:

- runs on its **own thread** with its own message loop, and its callback returns immediately (it only checks the keys and queues work for the main thread): if a callback stalls for more than ~300 ms Windows silently removes the hook, and until then it slows keyboard input system-wide;
- reacts only to `P` and `V` while the right Alt is down; everything else is passed through untouched and nothing is recorded;
- marks its own injected key presses (`dwExtraInfo = OwnTag`) and ignores them.

**Right Alt and AltGr.** On layouts with AltGr (Spanish, Russian "typewriter", etc.) Windows also "presses" the left Ctrl when you press the right Alt. Measured on a live system:

| Layout | Events when the right Alt is pressed |
|---|---|
| US | only `RMENU` |
| Spanish | `LCONTROL` with scan code `0x21D`, then `RMENU` |
| Russian (typewriter) | the same: `LCONTROL` with scan code `0x21D`, then `RMENU` |

So the hook treats only `LCONTROL` with a scan code other than `0x21D`, and `RCONTROL`, as a *real* Ctrl. `Right Alt + P/V` fires when the right Alt is down and the left Alt, Win and a real Ctrl are not. That is why `Ctrl + Alt + P` (including with the right Alt) still opens the window: the hook leaves it alone.

Key state is read from `GetAsyncKeyState` at the moment `P`/`V` goes down rather than accumulated from events, so there are no "stuck" keys if an event is lost.

**The masking key.** If you press and release Alt with nothing in between, many programs (Notepad, Firefox…) open their menu and take focus away from the text field. So after swallowing `P`/`V` the hook presses and releases a meaningless key, `VK 0xE8` — the same trick AutoHotkey uses.

The hook also swallows auto-repeat and the key release; if a release is lost, the state resets on the next press.

### Character injection (`Typer`)

- Characters are sent with `SendInput` and `KEYEVENTF_UNICODE`: the application receives a ready Unicode character instead of a key code, so the current layout does not matter. Browsers, Notepad and most programs take this as an ordinary key press.
- Before typing, the worker waits until Alt, Ctrl, Shift and Win are physically released (`GetAsyncKeyState`, up to 1.5 s), otherwise the application would see `Alt + character`. Then it waits another 30 ms for the application to notice.
- Before **each** character it checks that the active window belongs to the same process as at the time of the shortcut. If focus moved (say, another application popped up) the rest is not typed — a password must not land in the wrong window. The process is compared, not the window: a browser's active window may change within the process (popups).
- The pause between characters is `Thread.Sleep(typedelay)`. The default system timer step is 15.6 ms, so `timeBeginPeriod(1)` is on while typing — then 8 ms really is 8 ms.
- The size of `INPUT` depends on the bitness (40 bytes in a 64-bit process): it is a union of `MOUSEINPUT`, `KEYBDINPUT` and `HARDWAREINPUT`, and all three must be declared.
- `Typer.Clean` prepares clipboard text: trims the ends, turns line breaks and tabs into a space (a real `Enter` could submit a form), drops control characters.

### Scenarios (`TrayApp`)

- `QuickType` (`Right Alt + P`): generate → append to the file (the description is the active window's title) → copy to the clipboard → `TypeAsync`.
- `QuickType(again)` (`Right Alt + Shift + P`): takes `lastPassword`, writes nothing.
- `QuickPaste` (`Right Alt + V`): reads the clipboard, cleans the text, checks the 500-character limit → `TypeAsync`.
- `TypeAsync` types on a worker thread and calls `done(problem)` on the main thread when finished: a toast with the result. The `typing` flag (via `Interlocked`) prevents two typing jobs at once; a press during typing is ignored.
- If the generator window itself is active, quick typing does nothing.
- Errors in quick mode are shown as a red toast, not a dialog: a dialog would take focus from the field being typed into.

## Clipboard (`Clip`)

The password is placed with the formats `ExcludeClipboardContentFromMonitorProcessing`, `CanIncludeInClipboardHistory` (0) and `CanUploadToCloudClipboard` (0), so it stays out of Windows clipboard history (`Win + V`) and the cloud clipboard. Writes are retried if another program is holding the clipboard.

## The password file (`Storage`)

- The file is opened with `FileShare.ReadWrite`, so it can stay open in an editor.
- A new (empty) file gets a BOM first. If an existing file doesn't end with a line break (someone edited it by hand), one is added before the entry.
- After writing, `Flush(true)` pushes the data to disk.

## Gotchas

- **The tray icon tooltip is at most 63 characters.** Otherwise `NotifyIcon.Text` throws `ArgumentOutOfRangeException` and the program crashes at start-up. The code truncates the text and the localization test checks every language, but keep it in mind when editing.
- **Source encoding.** `.cs` and `.ps1` files are UTF-8 **with BOM** and the compiler runs with `/codepage:65001`. Without that, non-ASCII text turns into mojibake on systems whose ANSI code page is not the right one.
- **`System.Windows.Forms.Timer` vs `System.Threading.Timer`** — two different `Timer`s; in files with both `using`s write the full name.
- **Focus rights.** `SetForegroundWindow` is allowed only if the process received the user's last input. That holds for a hotkey, a tray click and a menu click; not for synthetic calls from other programs.
- **`ToolStrip` menus are invisible to UI Automation** — MSAA (`accName`, `accDoDefaultAction`) was what worked for automated menu checks.
- **A running exe can't be overwritten but can be renamed** — the file swap in `build.bat` relies on it.
- **Windows may keep a freshly started exe locked** for a while after the process exits (security software scanning it) — hence the rename trick again.

## What the automated tests don't cover

The tests cover the generator, the clipboard-text cleanup and the translation table. The UI, hooks, typing and focus are checked by hand with the [checklist](../CONTRIBUTING.md#manual-checklist): they depend on a real Windows desktop (active windows, layouts, process rights), and there was no safe way to automate them without risking typing into someone's windows.
