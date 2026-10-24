// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.Globalization;

namespace PassGen
{
    // Локализация интерфейса: английский, испанский, русский.
    // Язык берётся из настроек (lang= в PassGen.ini) или, если там «auto», из языка интерфейса Windows.
    // Чтобы добавить язык: допишите код в Codes, название в Names и колонку в Table (порядок колонок = порядок кодов).
    static class Loc
    {
        public const string Auto = "auto";
        public static readonly string[] Codes = { "en", "es", "ru" };
        public static readonly string[] Names = { "English", "Español", "Русский" };    // как язык называется на самом себе

        static Dictionary<string, string>[] tables;
        static int index;                                         // номер текущего языка в Codes

        public static string Code
        {
            get { return Codes[index]; }
        }

        // setting — «auto» или код языка. Незнакомое значение считается «auto».
        public static void Init(string setting)
        {
            Build();
            string code = setting;
            if (string.IsNullOrEmpty(code) || Array.IndexOf(Codes, code) < 0) code = Detect();
            index = Array.IndexOf(Codes, code);
        }

        // Язык интерфейса Windows → один из поддерживаемых; если такого нет — английский.
        public static string Detect()
        {
            string two = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
            return Array.IndexOf(Codes, two) >= 0 ? two : "en";
        }

        public static string T(string key)
        {
            Build();
            string s;
            if (tables[index].TryGetValue(key, out s)) return s;
            if (tables[0].TryGetValue(key, out s)) return s;      // перевода нет — берём английский
            return key;
        }

        public static string T(string key, params object[] args)
        {
            return string.Format(T(key), args);
        }

        // Слово во множественном числе. Формы разделены «|»: «знак|знака|знаков» (ru) или «character|characters» (en, es).
        public static string Plural(int n, string key)
        {
            string[] forms = T(key).Split('|');
            int i;
            if (Code == "ru")
            {
                int m = n % 100;
                if (m >= 11 && m <= 14) i = 2;
                else if (n % 10 == 1) i = 0;
                else if (n % 10 >= 2 && n % 10 <= 4) i = 1;
                else i = 2;
            }
            else
            {
                i = n == 1 ? 0 : 1;
            }
            return forms[Math.Min(i, forms.Length - 1)];
        }

        static void Build()
        {
            if (tables != null) return;
            Dictionary<string, string>[] t = new Dictionary<string, string>[Codes.Length];
            for (int c = 0; c < Codes.Length; c++)
            {
                t[c] = new Dictionary<string, string>();
                foreach (string[] row in Table) t[c][row[0]] = row[c + 1];
            }
            tables = t;
        }

        // Колонки: ключ | en | es | ru
        internal static readonly string[][] Table =
        {
            // — окно генератора —
            new string[] { "popup.cue", "Description (optional): site, login…", "Descripción (opcional): sitio, usuario…", "Описание (необязательно): сайт, логин…" },
            new string[] { "popup.length", "Length", "Longitud", "Длина" },
            new string[] { "popup.bits", "≈{0} bits", "≈{0} bits", "≈{0} бит" },
            new string[] { "popup.symbols", "Symbols", "Símbolos", "Спецзнаки" },
            new string[] { "popup.min", "Minimum:", "Mínimo:", "Минимум:" },
            new string[] { "popup.exact", "Exactly:", "Exacto:", "Ровно:" },
            new string[] { "popup.easy", "Easy to type", "Fácil de teclear", "Удобно вводить" },
            new string[] { "popup.blocks", "Blocks «-»", "Bloques «-»", "Блоки «-»" },
            new string[] { "popup.letter", "Start with a letter", "Empezar con letra", "Начинать с буквы" },
            new string[] { "popup.save", "Copy and save   (Enter)", "Copiar y guardar   (Enter)", "Скопировать и сохранить   (Enter)" },
            new string[] { "popup.help", "Keyboard shortcuts — F1", "Atajos de teclado — F1", "Справка по клавишам — F1" },
            new string[] { "tip.new", "Another password (F5).  Length: Ctrl+↑ / Ctrl+↓", "Otra contraseña (F5).  Longitud: Ctrl+↑ / Ctrl+↓", "Другой пароль (F5).  Длина: Ctrl+↑ / Ctrl+↓" },
            new string[] { "tip.bits", "Strength estimate in bits: the higher, the stronger (80 or more is excellent)", "Estimación de la fortaleza en bits: cuanto más, mejor (a partir de 80 es excelente)", "Оценка стойкости в битах: чем больше, тем надёжнее (от 80 — отлично)" },
            new string[] { "tip.symset", "Which special characters may be used: type your own or pick from the list", "Qué caracteres especiales se pueden usar: escriba los suyos o elija de la lista", "Какие спецзнаки можно использовать: впишите свои или выберите из списка" },
            new string[] { "tip.min", "How many special characters the password needs (a minimum; in “Easy to type” mode — exactly this many)", "Cuántos caracteres especiales debe tener la contraseña (mínimo; en el modo «Fácil de teclear», exactamente esa cantidad)", "Сколько спецзнаков нужно в пароле (минимум; в режиме «Удобно вводить» — ровно столько)" },
            new string[] { "tip.easy", "A password that is easier to type by hand:\nno look-alike characters (0 O 1 l I) and no “hard” special characters,\nfew special characters — exactly as many as set to the right of the list", "Una contraseña más fácil de teclear a mano:\nsin caracteres parecidos (0 O 1 l I) ni caracteres especiales «difíciles»,\npocos caracteres especiales: exactamente los indicados a la derecha de la lista", "Пароль, который проще набрать руками:\nбез похожих знаков (0 O 1 l I) и «трудных» спецзнаков,\nспецзнаков мало — ровно столько, сколько указано справа от списка" },
            new string[] { "tip.blocks", "Split into groups separated by “-” (hyphens count toward the total length)", "Dividir en grupos separados por «-» (los guiones cuentan en la longitud total)", "Разбить на группы через «-» (дефисы входят в общую длину)" },
            new string[] { "tip.letter", "The first character is a letter (some sites require it)", "El primer carácter es una letra (algunos sitios lo exigen)", "Первый знак — буква (так требуют некоторые сайты)" },

            // — справка по клавишам —
            new string[] { "help.title", "PassGen — keyboard shortcuts", "PassGen — atajos de teclado", "PassGen — справка по клавишам" },
            new string[] { "help.heading", "PassGen shortcuts", "Atajos de PassGen", "Клавиши PassGen" },
            new string[] { "help.anywhere", "In any program", "En cualquier programa", "В любой программе" },
            new string[] { "help.window", "In the generator window", "En la ventana del generador", "В окне генератора" },
            new string[] { "help.k.p", "Right Alt + P", "Alt derecho + P", "Правый Alt + P" },
            new string[] { "help.d.p", "new password: copy it, save it to the file and type it where the cursor is", "contraseña nueva: se copia, se guarda en el archivo y se escribe donde está el cursor", "новый пароль: скопировать, записать в файл и напечатать там, где курсор" },
            new string[] { "help.k.again", "Right Alt + Shift + P", "Alt derecho + Mayús + P", "Правый Alt + Shift + P" },
            new string[] { "help.d.again", "type the last password again (the “repeat password” field)", "escribir otra vez la última contraseña (el campo «repetir contraseña»)", "напечатать последний пароль ещё раз (поле «повторите пароль»)" },
            new string[] { "help.k.v", "Right Alt + V", "Alt derecho + V", "Правый Alt + V" },
            new string[] { "help.d.v", "type the clipboard contents character by character (e-mail, login, a code — where pasting is blocked)", "escribir el contenido del portapapeles carácter a carácter (correo, usuario, código: donde no se puede pegar)", "напечатать по символам содержимое буфера обмена (e-mail, логин, код — где вставка запрещена)" },
            new string[] { "help.k.open", "Ctrl + left Alt + P", "Ctrl + Alt izquierdo + P", "Ctrl + левый Alt + P" },
            new string[] { "help.d.open", "open the generator window (same as clicking the tray icon)", "abrir la ventana del generador (igual que hacer clic en el icono de la bandeja)", "открыть окно генератора (как клик по значку в трее)" },
            new string[] { "help.k.enter", "Enter", "Enter", "Enter" },
            new string[] { "help.d.enter", "copy and save", "copiar y guardar", "скопировать и сохранить" },
            new string[] { "help.k.esc", "Esc", "Esc", "Esc" },
            new string[] { "help.d.esc", "close without saving", "cerrar sin guardar", "закрыть без сохранения" },
            new string[] { "help.k.f5", "F5  or  Ctrl + R", "F5  o  Ctrl + R", "F5  или  Ctrl + R" },
            new string[] { "help.d.f5", "another password", "otra contraseña", "другой пароль" },
            new string[] { "help.k.len", "Ctrl + ↑  /  Ctrl + ↓", "Ctrl + ↑  /  Ctrl + ↓", "Ctrl + ↑  /  Ctrl + ↓" },
            new string[] { "help.d.len", "password length +1 / −1", "longitud de la contraseña +1 / −1", "длина пароля +1 / −1" },
            new string[] { "help.k.f1", "F1", "F1", "F1" },
            new string[] { "help.d.f1", "this help", "esta ayuda", "эта справка" },
            new string[] { "help.note", "Quick typing (Right Alt+P and Right Alt+V) can be switched on and off in the tray icon menu. It does not work in windows running as administrator.", "La escritura rápida (Alt derecho+P y Alt derecho+V) se activa y desactiva en el menú del icono de la bandeja. No funciona en ventanas ejecutadas como administrador.", "Быстрый ввод (правый Alt+P и правый Alt+V) включается и выключается в меню значка в трее. В окнах, запущенных от имени администратора, он не работает." },
            new string[] { "help.close", "Close", "Cerrar", "Закрыть" },

            // — меню значка в трее и подсказка значка —
            new string[] { "menu.generate", "Generate password", "Generar contraseña", "Сгенерировать пароль" },
            new string[] { "menu.openfile", "Open passwords file", "Abrir archivo de contraseñas", "Открыть файл с паролями" },
            new string[] { "menu.choosefile", "Save to…", "Guardar en…", "Куда сохранять…" },
            new string[] { "menu.quick", "Quick typing: Right Alt+P and Right Alt+V", "Escritura rápida: Alt derecho+P y Alt derecho+V", "Быстрый ввод: правый Alt+P и правый Alt+V" },
            new string[] { "menu.quick.tip", "Right Alt+P: a new password — copy it, save it to the file and type it where the cursor is.\nRight Alt+Shift+P: type the last password again (the “repeat password” field).\nRight Alt+V: type the clipboard contents character by character (e-mail, login, code — where pasting is blocked).", "Alt derecho+P: contraseña nueva — se copia, se guarda en el archivo y se escribe donde está el cursor.\nAlt derecho+Mayús+P: escribir otra vez la última contraseña (el campo «repetir contraseña»).\nAlt derecho+V: escribir el contenido del portapapeles carácter a carácter (correo, usuario, código: donde no se puede pegar).", "Правый Alt+P: новый пароль — скопировать, записать в файл и напечатать там, где курсор.\nПравый Alt+Shift+P: напечатать последний пароль ещё раз (поле «повторите пароль»).\nПравый Alt+V: напечатать по символам содержимое буфера обмена (e-mail, логин, код — где вставка запрещена)." },
            new string[] { "menu.autostart", "Start with Windows", "Iniciar con Windows", "Запускать вместе с Windows" },
            new string[] { "menu.language", "Language", "Idioma", "Язык" },
            new string[] { "menu.language.auto", "Automatic (system language)", "Automático (idioma del sistema)", "Автоматически (язык системы)" },
            new string[] { "menu.help", "Keyboard shortcuts", "Atajos de teclado", "Справка по клавишам" },
            new string[] { "menu.exit", "Exit", "Salir", "Выход" },
            new string[] { "tray.head", "PassGen — click", "PassGen — clic", "PassGen — клик" },
            new string[] { "tray.hotkey", " / Ctrl+Alt+P", " / Ctrl+Alt+P", " / Ctrl+Alt+P" },
            new string[] { "tray.window", ": window", ": ventana", ": окно" },
            new string[] { "tray.quick", ", Right Alt+P/V: type", ", Alt der.+P/V: escribir", ", правый Alt+P/V: ввод" },

            // — плашки и сообщения —
            new string[] { "toast.quickFail", "Could not enable quick typing (Right Alt+P / Right Alt+V)", "No se pudo activar la escritura rápida (Alt derecho+P / Alt derecho+V)", "Не удалось включить быстрый ввод (правый Alt+P / правый Alt+V)" },
            new string[] { "toast.noPassword", "No password yet — press Right Alt+P", "Aún no hay contraseña: pulse Alt derecho+P", "Пароля ещё нет — нажмите правый Alt+P" },
            new string[] { "toast.clipNoText", "The clipboard has no text", "El portapapeles no contiene texto", "В буфере обмена нет текста" },
            new string[] { "toast.clipTooLong", "The clipboard text is too long ({0} characters, up to {1} allowed)", "El texto del portapapeles es demasiado largo ({0} caracteres, máximo {1})", "В буфере слишком длинный текст ({0} знаков, можно до {1})" },
            new string[] { "toast.notTyped", "Not typed: {0}", "No se escribió: {0}", "Не введено: {0}" },
            new string[] { "toast.pasted", "✓  Typed from the clipboard: {0} {1}", "✓  Escrito desde el portapapeles: {0} {1}", "✓  Из буфера введено: {0} {1}" },
            new string[] { "unit.char", "character|characters", "carácter|caracteres", "знак|знака|знаков" },
            new string[] { "prob.noWindow", "no active window", "no hay ventana activa", "нет активного окна" },
            new string[] { "prob.keysHeld", "keys were not released — typing cancelled", "no se soltaron las teclas: escritura cancelada", "клавиши не отпущены — ввод отменён" },
            new string[] { "prob.windowChanged", "the window changed — typing cancelled", "la ventana cambió: escritura cancelada", "окно сменилось — ввод отменён" },
            new string[] { "prob.blocked", "the window does not accept input (running as administrator?)", "la ventana no acepta la entrada (¿se ejecuta como administrador?)", "окно не принимает ввод (запущено от администратора?)" },
            new string[] { "prob.interrupted", "the window changed — typing interrupted", "la ventana cambió: escritura interrumpida", "окно сменилось — ввод прерван" },
            new string[] { "toast.typeFailedCopied", "The password is on the clipboard, but {0}", "La contraseña está en el portapapeles, pero {0}", "Пароль в буфере, но {0}" },
            new string[] { "toast.typeFailed", "The password was not typed: {0}", "La contraseña no se escribió: {0}", "Пароль не введён: {0}" },
            new string[] { "toast.typedNotSaved", "Typed, but NOT saved to the file: {0}", "Escrita, pero NO guardada en el archivo: {0}", "Введён, но НЕ записан в файл: {0}" },
            new string[] { "toast.typedAgain", "✓  Password typed again", "✓  Contraseña escrita de nuevo", "✓  Пароль введён ещё раз" },
            new string[] { "toast.typedSaved", "✓  Password typed and saved", "✓  Contraseña escrita y guardada", "✓  Пароль введён и сохранён" },
            new string[] { "msg.copiedNotSaved", "The password was copied but NOT saved to the file:", "La contraseña se copió, pero NO se guardó en el archivo:", "Пароль скопирован, но НЕ записан в файл:" },
            new string[] { "msg.notCopiedNotSaved", "The password was NOT copied and NOT saved to the file:", "La contraseña NO se copió y NO se guardó en el archivo:", "Пароль НЕ скопирован и НЕ записан в файл:" },
            new string[] { "toast.copiedSaved", "✓  Copied and saved", "✓  Copiada y guardada", "✓  Скопировано и сохранено" },
            new string[] { "toast.savedNotCopied", "Saved, but copying to the clipboard failed", "Guardada, pero no se pudo copiar al portapapeles", "Сохранено, но в буфер скопировать не удалось" },
            new string[] { "dlg.saveTitle", "Where to save passwords", "Dónde guardar las contraseñas", "Куда сохранять пароли" },
            new string[] { "dlg.filter", "Text files (*.txt)|*.txt|All files (*.*)|*.*", "Archivos de texto (*.txt)|*.txt|Todos los archivos (*.*)|*.*", "Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*" },
            new string[] { "toast.savingTo", "Passwords are saved to “{0}”", "Las contraseñas se guardan en «{0}»", "Пароли пишутся в «{0}»" }
        };
    }
}
