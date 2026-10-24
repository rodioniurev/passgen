# Contributing to PassGen

Thanks for helping! Bug reports, ideas, translations and pull requests are welcome — in English, Spanish or Russian. *(Русская версия — [ниже](#по-русски).)*

## Reporting a bug or suggesting an idea

Open an [issue](https://github.com/rodioniurev/passgen/issues/new/choose) and fill in the template. For bugs it helps a lot to include:

- the PassGen version (right-click `PassGen.exe` → Properties → Details) and the Windows version;
- your keyboard layouts — they decide how the right Alt key behaves;
- for quick typing (`Right Alt + P/V`): the program or website where it fails, and whether that program runs as administrator;
- the antivirus you use, if it could interfere.

Please report security problems privately, as described in [SECURITY.md](SECURITY.md), not in a public issue.

## Environment

You need Windows with .NET Framework 4.x and any text editor or IDE. Visual Studio and the SDK are not required: the project is built with the compiler that ships with Windows (`build.bat`). See [docs/building.md](docs/building.md). How the code is organized is described in [docs/architecture.md](docs/architecture.md).

## Code rules

- **C# 5 only.** Newer language features (`$"…"`, `?.`, `=>` members, `nameof`, `out var`, …) are not available in the in-box compiler. The full list is in [docs/building.md](docs/building.md#language-limits).
- **Encoding.** `.cs` and `.ps1` files are UTF-8 **with BOM** (they contain Cyrillic and accented text); `build.bat` is ASCII with CRLF. This is enforced by `.editorconfig` and `.gitattributes`.
- **No hard-coded UI text.** Every string the user can see goes through `Loc.T(…)` and lives in the translation table in [`src/Loc.cs`](src/Loc.cs), with all three languages filled in.
- **Comments.** Code comments are in Russian; English comments in pull requests are just as welcome.
- **Project principles:** one exe, no installer, no third-party libraries, no network code, no telemetry. A pull request that breaks these can't be accepted.
- **Be careful with the keyboard hook:** `KeyHook.Callback` must return instantly, write nothing to disk and wait for nothing — otherwise Windows removes the hook and typing slows down system-wide.
- **The tray icon tooltip is at most 63 characters** in every language (otherwise the program crashes at start-up; the localization tests check this).

## Translations

All interface text is in one table, [`src/Loc.cs`](src/Loc.cs): a row per string, a column per language (`key | en | es | ru`). To improve a translation, edit its cell.

To add a language:

1. Add its two-letter code (as in `CultureInfo.TwoLetterISOLanguageName`) to `Loc.Codes` and its own name to `Loc.Names`.
2. Add a column with the translation to **every** row of `Loc.Table`, in the same order as `Codes`.
3. If the language has more than two plural forms (like Russian), extend `Loc.Plural`.
4. Run `build.bat` and `tests\LocalizationTests.ps1`: it checks that nothing is missing, that the `{0}` placeholders match and that the tray tooltip fits.
5. Open the generator window, the help window and the tray menu in the new language — longer texts must not be clipped.
6. Optionally add `README.<code>.md` and screenshots in `docs/img/<code>/`.

## Checks before a pull request

1. `build.bat` finishes without warnings or errors.
2. The automated tests pass:

   ```bat
   powershell -NoProfile -ExecutionPolicy Bypass -File tests\GeneratorTests.ps1
   powershell -NoProfile -ExecutionPolicy Bypass -File tests\LocalizationTests.ps1
   ```

3. If you changed the UI, the hooks, focus handling or typing, go through the [manual checklist](#manual-checklist).
4. The documentation is updated: `README.md`, `README.es.md` and `README.ru.md` if user-visible behavior changed, and [CHANGELOG.md](CHANGELOG.md).
5. A new password-generation rule comes with a new check in `tests\GeneratorTests.ps1`; a new UI string comes with all three translations.

### Manual checklist

The UI and typing depend on a real Windows desktop, so automated tests don't cover them. Go through this list (only the items your change touches are needed):

- [ ] The window opens on a tray icon click and on `Ctrl + left Alt + P`; changing a setting changes the password at once; `F1` opens the help.
- [ ] `Enter` copies the password and appends a line to the file; after `Enter` and `Esc` the caret returns to the program you were in (try Notepad, with the window opened both by clicking the icon and by the shortcut).
- [ ] `Right Alt + P` types the password into Notepad and into a browser (including a field that blocks pasting), appends a line to the file and puts the password on the clipboard; Notepad's menu does not open.
- [ ] `Right Alt + Shift + P` types the same password again, and no new line appears in the file.
- [ ] `Right Alt + V` types the clipboard: an e-mail address, Cyrillic text, text with several lines (line breaks become spaces).
- [ ] All of the above works on a layout without AltGr (US English) and with AltGr (Russian "typewriter", Spanish).
- [ ] `Ctrl + left Alt + P` and `Ctrl + right Alt + P` still open the window, and left `Alt + P` does nothing.
- [ ] `PassGen.exe /exit` closes the program and the tray icon disappears at once.
- [ ] The quick typing switch in the tray menu turns the hook on and off.
- [ ] Tray menu → Language lists *Automatic*, English, Español and Русский; choosing one switches the generator window, the help window, the tray menu and the tooltip immediately, and nothing is clipped; `lang=` is saved to `PassGen.ini`.

## Pull requests

- One task per pull request; explain **what** you change and **why**.
- For UI changes, attach a screenshot.
- Never add `passwords.txt`, `PassGen.ini` or built `exe` files (they are in `.gitignore`).

## По-русски

Сообщения об ошибках, идеи, переводы и pull request'ы приветствуются на русском, английском или испанском.

- **Сборка:** Windows + .NET Framework 4.x, запустите `build.bat` (штатный компилятор C# 5, SDK не нужен). Подробности — [docs/building.ru.md](docs/building.ru.md), устройство кода — [docs/architecture.ru.md](docs/architecture.ru.md).
- **Правила кода:** только C# 5; файлы `.cs` и `.ps1` — UTF-8 **с BOM**; без сторонних библиотек и сетевого кода; обработчик хука клавиатуры должен отрабатывать мгновенно; подсказка значка в трее — не длиннее 63 знаков. Комментарии в коде — на русском (английские в pull request'ах тоже годятся).
- **Тексты интерфейса** не пишутся в формах, а берутся через `Loc.T(…)` из таблицы переводов в [`src/Loc.cs`](src/Loc.cs); новый язык — это код в `Loc.Codes`, название в `Loc.Names` и одна колонка в `Loc.Table`. После правок запустите `tests\LocalizationTests.ps1`.
- **Перед pull request'ом:** `build.bat` без ошибок, проходят `tests\GeneratorTests.ps1` и `tests\LocalizationTests.ps1`, пройдена ручная проверка (если менялись интерфейс, хуки, фокус или ввод), обновлены READMEs и [CHANGELOG.md](CHANGELOG.md).
- **Уязвимости** — не в открытый issue, а как описано в [SECURITY.md](SECURITY.md).

### Ручная проверка

Интерфейс и ввод привязаны к реальному рабочему столу Windows, поэтому автотестами не покрыты. Достаточно пунктов, которых коснулись изменения:

- [ ] Окно открывается по клику на значок и по `Ctrl + левый Alt + P`, настройки меняют пароль сразу, `F1` открывает справку.
- [ ] `Enter` копирует пароль и записывает строку в файл; после `Enter` и `Esc` курсор возвращается в прежнюю программу (проверьте на Блокноте, когда окно открыто и кликом по значку, и горячей клавишей).
- [ ] `Right Alt + P` печатает пароль в Блокнот и в браузер (в том числе в поле, куда запрещена вставка), пишет строку в файл, кладёт пароль в буфер; меню Блокнота при этом не открывается.
- [ ] `Right Alt + Shift + P` печатает тот же пароль, новой записи в файле нет.
- [ ] `Right Alt + V` печатает буфер: e-mail, кириллицу, текст из нескольких строк (переводы строк становятся пробелами).
- [ ] Всё выше работает на раскладке без AltGr (английская США) и с AltGr (русская «машинопись», испанская).
- [ ] `Ctrl + левый Alt + P` и `Ctrl + правый Alt + P` по-прежнему открывают окно, а левый `Alt + P` ничего не делает.
- [ ] `PassGen.exe /exit` закрывает программу, а значок исчезает из трея сразу.
- [ ] Переключатель быстрого ввода в меню значка включает и выключает хук.
- [ ] Меню значка → «Язык» содержит «Автоматически», English, Español и Русский; выбор мгновенно переключает окно генератора, справку, меню и подсказку значка, ничего не обрезается; `lang=` сохраняется в `PassGen.ini`.
