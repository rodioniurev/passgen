// SPDX-License-Identifier: MIT
using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace PassGen
{
    // ───────────────────────── настройки (PassGen.ini рядом с exe) ─────────────────────────

    class Settings
    {
        public string PasswordFile;
        public int Length = 16;
        public bool Upper = true, Lower = true, Digits = true, Symbols = true;
        public string SymbolSet = Gen.DefaultSymbols;
        public int MinSymbols = 1;
        public bool Easy, Blocks, StartLetter;
        public bool QuickType = true;          // Right Alt+P: сгенерировать, скопировать и напечатать там, где курсор
        public bool AutoDesc = true;           // в быстром режиме описание записи — заголовок окна, куда вводили пароль
        public int TypeDelay = 8;              // пауза между напечатанными знаками, мс
        public string Language = Loc.Auto;     // язык интерфейса: «auto» — как в Windows, иначе en / es / ru

        public static string AppDir
        {
            get { return Path.GetDirectoryName(Application.ExecutablePath); }
        }

        static string IniPath
        {
            get { return Path.Combine(AppDir, "PassGen.ini"); }
        }

        public static Settings Load()
        {
            Settings s = new Settings();
            s.PasswordFile = Path.Combine(AppDir, "passwords.txt");
            try
            {
                int n;
                foreach (string line in File.ReadAllLines(IniPath, Encoding.UTF8))
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                    string val = line.Substring(eq + 1).Trim();
                    switch (key)
                    {
                        case "file": if (val.Length > 0) s.PasswordFile = val; break;
                        case "length": if (int.TryParse(val, out n)) s.Length = Math.Max(4, Math.Min(128, n)); break;
                        case "upper": s.Upper = val != "0"; break;
                        case "lower": s.Lower = val != "0"; break;
                        case "digits": s.Digits = val != "0"; break;
                        case "symbols": s.Symbols = val != "0"; break;
                        case "symset": s.SymbolSet = val; break;
                        case "minsym": if (int.TryParse(val, out n)) s.MinSymbols = Math.Max(1, Math.Min(8, n)); break;
                        case "easy": s.Easy = val == "1"; break;
                        case "blocks": s.Blocks = val == "1"; break;
                        case "letter": s.StartLetter = val == "1"; break;
                        case "quick": s.QuickType = val != "0"; break;
                        case "autodesc": s.AutoDesc = val != "0"; break;
                        case "typedelay": if (int.TryParse(val, out n)) s.TypeDelay = Math.Max(0, Math.Min(200, n)); break;
                        case "lang": s.Language = val.ToLowerInvariant(); break;
                    }
                }
            }
            catch (Exception) { }
            return s;
        }

        public void Save()
        {
            try
            {
                File.WriteAllLines(IniPath, new string[]
                {
                    "file=" + PasswordFile,
                    "length=" + Length.ToString(CultureInfo.InvariantCulture),
                    "upper=" + (Upper ? "1" : "0"),
                    "lower=" + (Lower ? "1" : "0"),
                    "digits=" + (Digits ? "1" : "0"),
                    "symbols=" + (Symbols ? "1" : "0"),
                    "symset=" + SymbolSet,
                    "minsym=" + MinSymbols.ToString(CultureInfo.InvariantCulture),
                    "easy=" + (Easy ? "1" : "0"),
                    "blocks=" + (Blocks ? "1" : "0"),
                    "letter=" + (StartLetter ? "1" : "0"),
                    "quick=" + (QuickType ? "1" : "0"),
                    "autodesc=" + (AutoDesc ? "1" : "0"),
                    "typedelay=" + TypeDelay.ToString(CultureInfo.InvariantCulture),
                    "lang=" + Language
                }, new UTF8Encoding(true));
            }
            catch (Exception) { }
        }
    }
}
