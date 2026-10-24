# Security policy

[Español](#resumen-en-español) · [Русский](#кратко-по-русски)

## The security model in short

PassGen is **not a password manager** — it is a generator with a log. To stay simple, it deliberately stores the generated passwords **in clear text** in an ordinary text file (`passwords.txt`). Any program running as you can read that file. So:

- keep the file on an encrypted drive (BitLocker or similar);
- don't put it in cloud-synced folders (OneDrive, Dropbox, Google Drive…);
- never publish or commit `passwords.txt` or `PassGen.ini` — both are already in `.gitignore`;
- the description of an entry made by quick typing is the title of the active window, which may contain sensitive data; turn it off with `autodesc=0`;
- the password stays on the clipboard until something else replaces it. It is kept out of the Windows clipboard history (`Win + V`) and the cloud clipboard with special clipboard formats, but any program can read the ordinary clipboard.

### What the program does with the keyboard

For `Right Alt + P/V` the program installs a global low-level keyboard hook. It:

- looks only at `P`, `V` and the modifier keys (`Alt`, `Ctrl`, `Shift`, `Win`) to recognize the shortcut;
- **records nothing** and sends nothing anywhere — the program contains no network code at all;
- passes every other key press through untouched.

This is easy to verify: the hook code is in [`src/QuickInput.cs`](src/QuickInput.cs) (class `KeyHook`). Installing a hook and typing text with `SendInput` can trigger false alarms in antivirus products. If that matters to you, build the exe yourself ([docs/building.md](docs/building.md)) — all you need is Windows.

Text typed with `SendInput` is visible to any program that intercepts the keyboard itself (keyloggers), exactly as if you had typed it on the keyboard.

## Supported versions

Fixes are released for the latest version in [Releases](https://github.com/rodioniurev/passgen/releases).

## Reporting a vulnerability

Please **do not open a public issue** with details. Instead:

1. Use [**Report a vulnerability**](https://github.com/rodioniurev/passgen/security/advisories/new) on the repository's **Security** tab (a private report). If that feature is not available, open a plain issue without technical details asking for a private channel, and we will agree on one.
2. Describe what can be done, how to reproduce it, and on which version.

We will try to answer within a reasonable time and, if you don't mind, credit you in the changelog.

## Resumen en español

PassGen **no es un gestor de contraseñas**: por diseño guarda las contraseñas generadas **en texto claro** en `passwords.txt`. Guarde ese archivo en un disco cifrado, fuera de carpetas sincronizadas con la nube y fuera del control de versiones (`passwords.txt` y `PassGen.ini` ya están en `.gitignore`). La descripción de las entradas del modo rápido es el título de la ventana activa; se desactiva con `autodesc=0`.

El gancho global de teclado de `Right Alt + P/V` solo mira `P`, `V` y las teclas modificadoras, no registra nada y el programa no contiene código de red (véase `KeyHook` en [`src/QuickInput.cs`](src/QuickInput.cs)). Algunos antivirus pueden dar falsas alarmas; puede compilar el exe usted mismo ([docs/building.md](docs/building.md)).

Informe de las vulnerabilidades en privado mediante [**Report a vulnerability**](https://github.com/rodioniurev/passgen/security/advisories/new) (pestaña **Security**), no en un issue público. Se publican correcciones para la última versión.

## Кратко по-русски

PassGen — **не менеджер паролей**: сгенерированные пароли он сознательно хранит **в открытом виде** в `passwords.txt`. Держите файл на зашифрованном диске, вне папок с облачной синхронизацией и вне системы контроля версий (`passwords.txt` и `PassGen.ini` уже в `.gitignore`). Описание записи при быстром вводе — заголовок активного окна; отключается параметром `autodesc=0`.

Глобальный хук клавиатуры для `Right Alt + P/V` смотрит только на `P`, `V` и клавиши-модификаторы, ничего не записывает, а сетевого кода в программе нет (см. `KeyHook` в [`src/QuickInput.cs`](src/QuickInput.cs)). Антивирусы могут срабатывать ложно; exe можно собрать самостоятельно ([docs/building.ru.md](docs/building.ru.md)).

Об уязвимостях сообщайте приватно через [**Report a vulnerability**](https://github.com/rodioniurev/passgen/security/advisories/new) (вкладка **Security**), а не в открытом issue. Исправления выпускаются для последней версии.
