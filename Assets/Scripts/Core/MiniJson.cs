using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DeepDrill.Core
{
    /// <summary>
    /// Small JSON reader for the design data. Core may not touch UnityEngine, and JsonUtility cannot
    /// read a top-level array anyway. Objects become dictionaries, arrays lists, numbers doubles.
    /// </summary>
    public static class MiniJson
    {
        public static object Parse(string text)
        {
            int i = 0;
            object value = ReadValue(text, ref i);
            SkipSpace(text, ref i);
            if (i != text.Length) throw new FormatException($"Trailing JSON at {i}");
            return value;
        }

        private static object ReadValue(string s, ref int i)
        {
            SkipSpace(s, ref i);
            switch (s[i])
            {
                case '{': return ReadObject(s, ref i);
                case '[': return ReadArray(s, ref i);
                case '"': return ReadString(s, ref i);
                case 't': i += 4; return true;
                case 'f': i += 5; return false;
                case 'n': i += 4; return null;
                default: return ReadNumber(s, ref i);
            }
        }

        private static Dictionary<string, object> ReadObject(string s, ref int i)
        {
            var map = new Dictionary<string, object>();
            i++;
            SkipSpace(s, ref i);
            if (s[i] == '}') { i++; return map; }
            while (true)
            {
                SkipSpace(s, ref i);
                string key = ReadString(s, ref i);
                SkipSpace(s, ref i);
                i++; // colon
                map[key] = ReadValue(s, ref i);
                SkipSpace(s, ref i);
                if (s[i++] == '}') return map;
            }
        }

        private static List<object> ReadArray(string s, ref int i)
        {
            var list = new List<object>();
            i++;
            SkipSpace(s, ref i);
            if (s[i] == ']') { i++; return list; }
            while (true)
            {
                list.Add(ReadValue(s, ref i));
                SkipSpace(s, ref i);
                if (s[i++] == ']') return list;
            }
        }

        private static string ReadString(string s, ref int i)
        {
            var sb = new StringBuilder();
            i++;
            while (s[i] != '"')
            {
                char c = s[i++];
                if (c != '\\') { sb.Append(c); continue; }
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'u':
                        sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        i += 4;
                        break;
                    default: sb.Append(e); break;
                }
            }
            i++;
            return sb.ToString();
        }

        private static double ReadNumber(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            return double.Parse(s.Substring(start, i - start), CultureInfo.InvariantCulture);
        }

        private static void SkipSpace(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }
    }
}
