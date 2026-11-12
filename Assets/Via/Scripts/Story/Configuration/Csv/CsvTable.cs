using System;
using System.Collections.Generic;

namespace Via.Story.Configuration
{
    // Header-addressed view over a CSV file so rows never depend on column order or Excel row numbers.
    public sealed class CsvTable
    {
        private readonly string name;
        private readonly Dictionary<string, int> columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string[]> rows = new List<string[]>();
        public string Name => name;
        public IReadOnlyList<string[]> Rows => rows;

        public CsvTable(string name, string text)
        {
            this.name = name;
            var parsed = text == null ? null : CsvParser.Parse(text);
            if (parsed == null || parsed.Count == 0) return;
            string[] header = parsed[0];
            for (int i = 0; i < header.Length; i++)
            {
                string key = header[i].Trim();
                if (key.Length > 0 && !columns.ContainsKey(key)) columns[key] = i;
            }
            for (int i = 1; i < parsed.Count; i++)
            {
                string[] row = parsed[i];
                if (row.Length == 1 && string.IsNullOrWhiteSpace(row[0])) continue;
                rows.Add(row);
            }
        }

        public int LineOf(int rowIndex) => rowIndex + 2;

        public bool TryGet(string[] row, string column, out string value)
        {
            value = string.Empty;
            if (!columns.TryGetValue(column, out int index)) return false;
            value = index < row.Length ? row[index].Trim() : string.Empty;
            return true;
        }

        public string Get(string[] row, string column) => TryGet(row, column, out string value) ? value : string.Empty;
    }
}
