using System;
using System.Linq;
using System.Text;

namespace GIMtransfer.read.DWGReader;

// Converts AutoCAD formatting codes (%%d, \H2;, \fArial|b0|i0;, \S...)
// into a universal tagged format (§DEG§, §HEIGHT:2§, §TTFONT:...§, §STACK:...§).
// Pure utility — no reader logic, no GimElement construction.
// ponytail: when element builders exist for Text/F_symbol, this stays as-is (it's format-only).

public static class DWG2DTextFormatter
{
    public static string ConvertAutoCADToUniversal(string autocadText)
    {
        if (string.IsNullOrEmpty(autocadText))
            return autocadText;

        var result = new StringBuilder();
        int i = 0;

        while (i < autocadText.Length)
        {
            if (autocadText[i] == '%' && i + 2 < autocadText.Length && autocadText[i + 1] == '%')
            {
                i += 2;
                char code = autocadText[i];
                i++;
                switch (code)
                {
                    case 'd': result.Append(Tag("DEG")); break;
                    case 'c': result.Append(Tag("DIA")); break;
                    case 'p': result.Append(Tag("PM")); break;
                    case 'u': result.Append(Tag("U_TOGGLE")); break;
                    case 'o': result.Append(Tag("O_TOGGLE")); break;
                    case '%': result.Append('%'); break;
                    default: result.Append("%%").Append(code); break;
                }
            }
            else if (autocadText[i] == '\\' && i + 1 < autocadText.Length)
            {
                i++;
                char code = autocadText[i];
                i++;
                switch (code)
                {
                    case 'H': result.Append(Tag(Param(autocadText, ref i, "HEIGHT"))); break;
                    case 'F': result.Append(Tag(Param(autocadText, ref i, "FONT"))); break;
                    case 'C': result.Append(Tag(Param(autocadText, ref i, "COLOR"))); break;
                    case 'A': result.Append(Tag(Param(autocadText, ref i, "ALIGN"))); break;
                    case 'W': result.Append(Tag(Param(autocadText, ref i, "WIDTH"))); break;
                    case 'T': result.Append(Tag(Param(autocadText, ref i, "TRACKING"))); break;
                    case 'Q': result.Append(Tag(Param(autocadText, ref i, "OBLIQUE"))); break;
                    case 'f': result.Append(Tag(FontAttr(autocadText, ref i))); break;
                    case 'S': result.Append(Tag(Stack(autocadText, ref i))); break;
                    case 'U': result.Append(Tag(Unicode(autocadText, ref i))); break;
                    case 'P': result.Append(Tag("BR")); break;
                    case 'X': result.Append(Tag("DIMWRAP")); break;
                    case 'N': result.Append(Tag("NEWCOL")); break;
                    case 'p': result.Append(Tag(Para(autocadText, ref i))); break;
                    case 'L': result.Append(Tag("U+")); break;
                    case 'O': result.Append(Tag("O+")); break;
                    case 'K': result.Append(Tag("S+")); break;
                    case 'l': result.Append(Tag("U-")); break;
                    case 'o': result.Append(Tag("O-")); break;
                    case 'k': result.Append(Tag("S-")); break;
                    case 'd': result.Append(Tag("DEG")); break;
                    case '-': result.Append(Tag("BULLET")); break;
                    case 'c':
                        if (i < autocadText.Length && char.IsDigit(autocadText[i]))
                            result.Append(Tag(Param(autocadText, ref i, "COLOR")));
                        else
                            result.Append(Tag("DIA"));
                        break;
                    case '\\': result.Append('\\'); break;
                    case '{': result.Append('{'); break;
                    case '}': result.Append('}'); break;
                    case '~': result.Append(' '); break;
                    default: Skip(autocadText, ref i); break;
                }
            }
            else if (autocadText[i] == '{' || autocadText[i] == '}')
            {
                i++;
            }
            else if (autocadText[i] == '\n')
            {
                result.Append(Tag("BR"));
                i++;
            }
            else if (autocadText[i] == '\r')
            {
                result.Append(Tag("BR"));
                if (i + 1 < autocadText.Length && autocadText[i + 1] == '\n')
                    i++;
                i++;
            }
            else
            {
                result.Append(autocadText[i]);
                i++;
            }
        }

        return result.ToString();

        string Tag(string content) => string.IsNullOrEmpty(content) ? "" : $"§{content}§";

        string Param(string txt, ref int idx, string name)
        {
            int s = idx;
            while (idx < txt.Length && txt[idx] != ';') idx++;
            string val = txt.Substring(s, idx - s);
            if (idx < txt.Length && txt[idx] == ';') idx++;
            return $"{name}:{val}";
        }

        string FontAttr(string txt, ref int idx)
        {
            int s = idx;
            while (idx < txt.Length && txt[idx] != ';') idx++;
            if (idx >= txt.Length) return "";

            string spec = txt.Substring(s, idx - s);
            idx++;

            var parts = spec.Split('|');
            string font = parts[0];
            string b = "0", it = "0", cp = "0", pt = "0";

            foreach (var p in parts.Skip(1))
            {
                if (p.StartsWith("b")) b = p.Substring(1);
                else if (p.StartsWith("i")) it = p.Substring(1);
                else if (p.StartsWith("c")) cp = p.Substring(1);
                else if (p.StartsWith("p")) pt = p.Substring(1);
            }

            return $"TTFONT:{font}|B:{b}|I:{it}|CP:{cp}|PT:{pt}";
        }

        string Stack(string txt, ref int idx)
        {
            int s = idx;
            while (idx < txt.Length && txt[idx] != ';') idx++;
            if (idx >= txt.Length) return "";

            string content = txt.Substring(s, idx - s);
            idx++;

            if (content.Contains("^")) return $"STACK:TOL:{content}";
            if (content.Contains("#")) return $"STACK:DIAG:{content.Replace("#", "/")}";
            return $"STACK:HORIZ:{content}";
        }

        string Unicode(string txt, ref int idx)
        {
            if (idx >= txt.Length || txt[idx] != '+')
                return "";
            idx++;
            if (idx + 4 > txt.Length) return "";
            string hex = txt.Substring(idx, 4);
            if (!hex.All(Uri.IsHexDigit)) return "";
            idx += 4;
            return $"UNI:{hex}";
        }

        string Para(string txt, ref int idx)
        {
            if (idx >= txt.Length) return "PM";
            char next = txt[idx];
            if (next != 'i' && next != 'x' && next != 't' && next != 'q')
                return "PM";
            idx++;
            int s = idx;
            while (idx < txt.Length && txt[idx] != ';') idx++;
            if (idx >= txt.Length) return "";
            string spec = txt.Substring(s, idx - s);
            idx++;
            return next switch
            {
                'i' => $"PARA:INDENT:{spec}",
                't' => $"PARA:TAB:{spec}",
                'q' => $"PARA:JUST:{spec}",
                'x' when spec.StartsWith("t") => $"PARA:TAB:{spec.Substring(1)}",
                'x' when spec.StartsWith("q") => $"PARA:JUST:{spec.Substring(1)}",
                'x' => $"PARA:FMT:{spec}",
                _ => ""
            };
        }

        void Skip(string txt, ref int idx)
        {
            while (idx < txt.Length && txt[idx] != ';' && txt[idx] != '\\') idx++;
            if (idx < txt.Length && txt[idx] == ';') idx++;
        }
    }
}
