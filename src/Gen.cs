// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace PassGen
{
    // ───────────────────────── генератор ─────────────────────────

    static class Gen
    {
        public const string DefaultSymbols = "!@#$%^&*-_=+?";
        public static readonly string[] SymbolPresets =
        {
            "!@#$%^&*-_=+?",
            "!@#$%&*",
            "-_.",
            "!@#$%^&*()-_=+[]{};:,.<>/?"
        };

        const string LowerAbc = "abcdefghijklmnopqrstuvwxyz";
        const string UpperAbc = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string DigitsAbc = "0123456789";
        const string Lookalikes = "0O1lI|";                        // путаются на глаз
        const string HardSymbols = "|\\/`'\"~^<>()[]{};:.,";       // неудобно вводить и разглядывать
        const string EasySymbols = "!@#$%&*-_+=?";

        static readonly RandomNumberGenerator Rng = RandomNumberGenerator.Create();

        // Равномерное число из [0, n) без смещения (отбрасываем «хвост» диапазона).
        static int Next(int n)
        {
            byte[] b = new byte[4];
            uint limit = uint.MaxValue - uint.MaxValue % (uint)n;
            uint v;
            do
            {
                Rng.GetBytes(b);
                v = BitConverter.ToUInt32(b, 0);
            } while (v >= limit);
            return (int)(v % (uint)n);
        }

        // log2 числа сочетаний C(n, k) — сколько бит даёт выбор мест для k спецзнаков.
        static double Log2Choose(int n, int k)
        {
            double r = 0;
            for (int i = 1; i <= k; i++) r += Math.Log((double)(n - k + i) / i, 2);
            return r;
        }

        static string Strip(string s, string remove)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in s)
                if (remove.IndexOf(c) < 0) sb.Append(c);
            return sb.ToString();
        }

        static string FilterSymbols(string set, bool easy, bool blocks)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in set)
            {
                if (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) || char.IsControl(c) || char.IsSurrogate(c)) continue;
                if (blocks && c == '-') continue;                          // дефис — разделитель блоков
                if (easy && HardSymbols.IndexOf(c) >= 0) continue;
                if (sb.ToString().IndexOf(c) >= 0) continue;               // без дублей
                sb.Append(c);
            }
            return sb.ToString();
        }

        static string SymbolAlphabet(Settings s)
        {
            string r = FilterSymbols(s.SymbolSet, s.Easy, s.Blocks);
            if (r.Length == 0) r = FilterSymbols(s.Easy ? EasySymbols : DefaultSymbols, s.Easy, s.Blocks);
            return r;
        }

        // Размеры блоков для «xxxxx-xxxxx-xxxx»: общая длина (с дефисами) строго равна total,
        // блоки по 4–5 знаков. Для коротких паролей блок один — дефисов нет.
        static int[] BlockSizes(int total)
        {
            int best = 1;
            double bestDiff = double.MaxValue;
            for (int b = 1; b <= total; b++)
            {
                double avg = (double)(total + 1) / b - 1;                  // знаков в блоке без дефисов
                if (avg < 2) break;
                double diff = Math.Abs(avg - 4.5);
                if (diff < bestDiff) { bestDiff = diff; best = b; }
            }
            int chars = total - (best - 1);
            int[] sizes = new int[best];
            for (int i = 0; i < best; i++)
                sizes[i] = chars / best + (i < chars % best ? 1 : 0);
            return sizes;
        }

        public static string Make(Settings s, out double bits)
        {
            string skip = s.Easy ? Lookalikes : "";
            string lower = s.Lower ? Strip(LowerAbc, skip) : "";
            string upper = s.Upper ? Strip(UpperAbc, skip) : "";
            string digits = s.Digits ? Strip(DigitsAbc, skip) : "";
            string symbols = s.Symbols ? SymbolAlphabet(s) : "";
            if (lower.Length + upper.Length + digits.Length + symbols.Length == 0)
                lower = Strip(LowerAbc, skip);

            int total = Math.Max(4, Math.Min(128, s.Length));
            int[] sizes = s.Blocks ? BlockSizes(total) : new int[] { total };
            int count = total - (sizes.Length - 1);                        // случайных знаков (без дефисов)

            // По одному знаку из каждого включённого набора (и MinSymbols спецзнаков) — обязательно.
            List<string> need = new List<string>();
            if (lower.Length > 0) need.Add(lower);
            if (upper.Length > 0) need.Add(upper);
            if (digits.Length > 0) need.Add(digits);
            int symForced = 0;
            if (symbols.Length > 0)
                for (int i = 0; i < Math.Max(1, s.MinSymbols); i++) { need.Add(symbols); symForced++; }
            if (need.Count > count)
            {
                symForced = Math.Max(0, symForced - (need.Count - count));
                need.RemoveRange(count, need.Count - count);
            }

            // Остальные знаки берём из общего набора. В режиме «Удобно вводить» спецзнаки в него не входят:
            // их ровно столько, сколько обязательных, — меньше переключений раскладки и Shift при наборе.
            string pool = lower + upper + digits + symbols;
            bool few = s.Easy && symbols.Length > 0 && lower.Length + upper.Length + digits.Length > 0;
            string rest = few ? lower + upper + digits : pool;
            char[] r = new char[count];
            for (int i = 0; i < count; i++)
            {
                string src = i < need.Count ? need[i] : rest;
                r[i] = src[Next(src.Length)];
            }

            // Тасуем (Фишер—Йетс), чтобы обязательные знаки не стояли в начале.
            for (int i = count - 1; i > 0; i--)
            {
                int j = Next(i + 1);
                char t = r[i]; r[i] = r[j]; r[j] = t;
            }

            string letters = lower + upper;
            if (s.StartLetter && letters.Length > 0 && letters.IndexOf(r[0]) < 0)
            {
                List<int> at = new List<int>();
                for (int i = 1; i < count; i++)
                    if (letters.IndexOf(r[i]) >= 0) at.Add(i);
                if (at.Count > 0)
                {
                    int j = at[Next(at.Count)];
                    char t = r[0]; r[0] = r[j]; r[j] = t;
                }
            }

            bits = few
                ? (count - symForced) * Math.Log(rest.Length, 2) + symForced * Math.Log(symbols.Length, 2) + Log2Choose(count, symForced)
                : count * Math.Log(pool.Length, 2);
            if (sizes.Length == 1) return new string(r);

            StringBuilder sb = new StringBuilder(total);
            int pos = 0;
            for (int b = 0; b < sizes.Length; b++)
            {
                if (b > 0) sb.Append('-');
                sb.Append(r, pos, sizes[b]);
                pos += sizes[b];
            }
            return sb.ToString();
        }
    }
}
