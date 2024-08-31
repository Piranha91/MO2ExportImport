using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MO2ExportImport
{
    public static class StringExtensions
    {
        // Removes the specified substring from the beginning of the string, if it exists
        public static string RemoveAtBeginning(string str, string toRemove)
        {
            if (str.StartsWith(toRemove, StringComparison.OrdinalIgnoreCase))
            {
                return str.Substring(toRemove.Length);
            }
            return str;
        }

        // Removes the specified substring from the end of the string, if it exists
        public static string RemoveAtEnd(string str, string toRemove)
        {
            if (str.EndsWith(toRemove, StringComparison.OrdinalIgnoreCase))
            {
                return str.Substring(0, str.Length - toRemove.Length);
            }
            return str;
        }
    }
}
