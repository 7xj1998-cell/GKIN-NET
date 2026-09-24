using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;

namespace GKIN
{
    /// <summary>
    /// Ghi đồng bộ attribute khung tên theo alias TOSO / MATO / TONGTO / TENTOBVE / TYLE.
    /// </summary>
    public static class AttributeSyncService
    {
        static readonly string[][] AliasGroups =
        {
            new[] { "STT", "SOTT", "TOSO" },
            new[] { "MSBV", "SBV", "MABV", "MASO", "MATO", "MS" },
            new[] { "BVS", "SOBV", "TONGTO" },
            new[] { "TENBVE", "TENBV", "TENBANVE", "TENTO", "TENTOBVE" },
            new[] { "TYLE", "TILE", "TL" }
        };

        public static Dictionary<string, string> Expand(IDictionary<string, string> values)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (values == null) return result;
            foreach (var pair in values)
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value == null) continue;
                result[pair.Key] = pair.Value;
                foreach (string alias in AliasesOf(pair.Key))
                    if (!result.ContainsKey(alias)) result[alias] = pair.Value;
            }
            return result;
        }

        public static IEnumerable<string> AliasesOf(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) yield break;
            foreach (var group in AliasGroups)
            {
                if (group.Any(x => x.Equals(tag, StringComparison.OrdinalIgnoreCase)))
                {
                    foreach (string alias in group) yield return alias;
                    yield break;
                }
            }
        }

        public static int Apply(IEnumerable<KeyValuePair<ObjectId, Dictionary<string, string>>> updates)
        {
            var expanded = new List<KeyValuePair<ObjectId, Dictionary<string, string>>>();
            if (updates == null) return 0;
            foreach (var update in updates)
                expanded.Add(new KeyValuePair<ObjectId, Dictionary<string, string>>(update.Key, Expand(update.Value)));
            return CadEngine.WriteAttributes(expanded);
        }

        public static string Title(string type, string from, string to, string fallbackName)
        {
            if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to)) return null;
            string name = string.IsNullOrWhiteSpace(fallbackName) ? "TRẮC DỌC TUYẾN" : fallbackName;
            return type == "TD" ? $"{name}: {from} -:- {to}" : $"{from} -:- {to}";
        }
    }
}
