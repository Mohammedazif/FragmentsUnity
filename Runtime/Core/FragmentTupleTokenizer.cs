using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FragmentsUnity
{
    /// <summary>Tokenizes one serialized attribute tuple like ["Name","Wall","IFCLABEL"] into its elements.</summary>
    internal static class FragmentTupleTokenizer
    {
        /// <summary>Splits the bracketed tuple into trimmed tokens; a JSON null becomes an empty token.</summary>
        internal static void SplitTuple(string input, List<string> tokens)
        {
            tokens.Clear();

            int len = input.Length;
            int i = 0;

            while (i < len && input[i] != '[')
            {
                i++;
            }
            if (i >= len)
            {
                return;
            }
            i++;

            while (i < len)
            {
                if (tokens.Count >= FragmentImportLimits.MaxTupleTokens)
                {
                    break;
                }

                while (i < len && (char.IsWhiteSpace(input[i]) || input[i] == ','))
                {
                    i++;
                }
                if (i >= len || input[i] == ']')
                {
                    break;
                }

                if (input[i] == '"')
                {
                    i++;
                    var token = new StringBuilder();
                    while (i < len)
                    {
                        char c = input[i];

                        if (c == '\\' && i + 1 < len)
                        {
                            char escaped = input[i + 1];
                            switch (escaped)
                            {
                                case 'n': token.Append('\n'); break;
                                case 't': token.Append('\t'); break;
                                case 'r': token.Append('\r'); break;
                                case 'b': token.Append('\b'); break;
                                case 'f': token.Append('\f'); break;
                                case 'u':
                                    if (i + 5 < len)
                                    {
                                        // Non-hex input yields 0 where the FragParser.cpp:247 strtoi keeps a leading-digit prefix — accepted divergence.
                                        int.TryParse(input.Substring(i + 2, 4), NumberStyles.HexNumber,
                                            CultureInfo.InvariantCulture, out int codeUnit);
                                        token.Append((char)codeUnit);
                                        i += 4;
                                    }
                                    break;
                                default: token.Append(escaped); break;
                            }
                            i += 2;
                            continue;
                        }

                        if (c == '"')
                        {
                            i++;
                            break;
                        }

                        token.Append(c);
                        i++;
                    }
                    tokens.Add(token.ToString());
                }
                else
                {
                    int start = i;
                    int depth = 0;
                    bool inString = false;

                    while (i < len)
                    {
                        char c = input[i];

                        if (inString)
                        {
                            if (c == '\\')
                            {
                                i += 2;
                                continue;
                            }
                            if (c == '"')
                            {
                                inString = false;
                            }
                        }
                        else if (c == '"')
                        {
                            inString = true;
                        }
                        else if (c == '[' || c == '{')
                        {
                            depth++;
                        }
                        else if (c == '}')
                        {
                            depth--;
                        }
                        else if (c == ']')
                        {
                            if (depth == 0)
                            {
                                break;
                            }
                            depth--;
                        }
                        else if (c == ',' && depth == 0)
                        {
                            break;
                        }

                        i++;
                    }

                    // The backslash skip can push i past len; FString::Mid clamps, mirrors FragParser.cpp:308.
                    string token = input.Substring(start, Math.Min(i, len) - start).Trim();
                    if (token.Equals("null", StringComparison.OrdinalIgnoreCase))
                    {
                        token = string.Empty;
                    }
                    tokens.Add(token);
                }
            }
        }
    }
}
