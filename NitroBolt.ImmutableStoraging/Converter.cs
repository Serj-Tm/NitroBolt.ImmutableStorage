using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NitroBolt.ImmutableStoraging
{
    class Converter
    {
        public static int? ToInt(string text)
        {
            if (String.IsNullOrEmpty(text))
                return null;
            var i = 0;
            var ch = text[i];
            for (;;)
            {
                if (!Char.IsWhiteSpace(text[i]))
                    break;
                ++i;
                if (i >= text.Length)
                    return null;
                ch = text[i];
            }
            bool? isSign = null;
            switch (ch)
            {
                case '+':
                    i++;
                    isSign = true;
                    break;
                case '-':
                    i++;
                    isSign = false;
                    break;
            }
            if (isSign != null)
            {
                if (i >= text.Length)
                    return null;
                ch = text[i];
                for (;;)
                {
                    if (!Char.IsWhiteSpace(text[i]))
                        break;
                    ++i;
                    if (i >= text.Length)
                        return null;
                    ch = text[i];
                }
            }
            var v = 0;
            for (;;)
            {
                if (Char.IsDigit(ch))
                {
                    var d = (ch - '0');
                    v = 10*v + (isSign == false ? -d : d);
                }
                else if (!Char.IsWhiteSpace(ch)) //error
                    return null;
                ++i;
                if (i >= text.Length)
                    return v;
                ch = text[i];
            }
        }

        public static long? ToLong(string text)
        {
            if (String.IsNullOrEmpty(text))
                return null;
            var i = 0;
            var ch = text[i];
            for (;;)
            {
                if (!Char.IsWhiteSpace(text[i]))
                    break;
                ++i;
                if (i >= text.Length)
                    return null;
                ch = text[i];
            }
            bool? isSign = null;
            switch (ch)
            {
                case '+':
                    i++;
                    isSign = true;
                    break;
                case '-':
                    i++;
                    isSign = false;
                    break;
            }
            if (isSign != null)
            {
                if (i >= text.Length)
                    return null;
                ch = text[i];
                for (;;)
                {
                    if (!Char.IsWhiteSpace(text[i]))
                        break;
                    ++i;
                    if (i >= text.Length)
                        return null;
                    ch = text[i];
                }
            }
            long v = 0;
            for (;;)
            {
                if (Char.IsDigit(ch))
                {
                    var d = (ch - '0');
                    v = 10*v + (isSign == false ? -d : d);
                }
                else if (!Char.IsWhiteSpace(ch)) //error
                    return null;
                ++i;
                if (i >= text.Length)
                    return v;
                ch = text[i];
            }
        }
    }
}
