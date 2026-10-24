// SPDX-License-Identifier: MIT
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace PassGen
{
    // ───────────────────────── файл, буфер обмена, автозапуск ─────────────────────────

    static class Storage
    {
        static readonly UTF8Encoding Utf8Bom = new UTF8Encoding(true);

        // Дописывает строку в конец файла (UTF-8 с BOM, чтобы Блокнот не путал кодировку).
        public static void Append(string path, string line)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            using (FileStream fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite))
            {
                string prefix = "";
                if (fs.Length == 0)
                {
                    byte[] bom = Utf8Bom.GetPreamble();
                    fs.Write(bom, 0, bom.Length);
                }
                else
                {
                    fs.Seek(-1, SeekOrigin.End);
                    if (fs.ReadByte() != '\n') prefix = "\r\n";            // файл правили руками и не закончили строку
                }
                fs.Seek(0, SeekOrigin.End);
                byte[] data = Utf8Bom.GetBytes(prefix + line + "\r\n");
                fs.Write(data, 0, data.Length);
                fs.Flush(true);
            }
        }

        public static void Touch(string path)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            using (new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite)) { }
        }
    }

    static class Clip
    {
        public static bool Copy(string text)
        {
            try
            {
                DataObject d = new DataObject();
                d.SetText(text, TextDataFormat.UnicodeText);
                // Не пускаем пароль в историю буфера Windows (Win+V) и в облачный буфер.
                d.SetData("ExcludeClipboardContentFromMonitorProcessing", new MemoryStream(new byte[4]));
                d.SetData("CanIncludeInClipboardHistory", new MemoryStream(new byte[4]));
                d.SetData("CanUploadToCloudClipboard", new MemoryStream(new byte[4]));
                Clipboard.SetDataObject(d, true, 10, 50);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // Текст из буфера обмена; null — там не текст (картинка, файлы) или буфер занят другой программой.
        public static string GetText()
        {
            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    return Clipboard.ContainsText() ? Clipboard.GetText(TextDataFormat.UnicodeText) : null;
                }
                catch (Exception)
                {
                    Thread.Sleep(40);                           // буфер на мгновение занят — пробуем ещё
                }
            }
            return null;
        }
    }

    static class Autostart
    {
        const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string ValueName = "PassGen";

        public static bool IsOn()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(KeyPath))
                    return k != null && k.GetValue(ValueName) != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static void Set(bool on)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(KeyPath))
                {
                    if (on) k.SetValue(ValueName, "\"" + Application.ExecutablePath + "\" /autostart");
                    else k.DeleteValue(ValueName, false);
                }
            }
            catch (Exception) { }
        }
    }
}
