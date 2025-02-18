using System.Text.RegularExpressions;

namespace SQLServerAdmin.Services
{
    /// <summary>
    /// Service für die Formatierung von SQL-Queries
    /// </summary>
    public class QueryFormatterService
    {
        private static readonly string[] Keywords = new[]
        {
            "SELECT", "FROM", "WHERE", "INSERT", "UPDATE", "DELETE",
            "CREATE", "ALTER", "DROP", "GROUP BY", "ORDER BY", "HAVING",
            "JOIN", "INNER JOIN", "LEFT JOIN", "RIGHT JOIN", "FULL JOIN",
            "UNION", "UNION ALL", "INTERSECT", "EXCEPT"
        };

        /// <summary>
        /// Formatiert eine SQL-Query
        /// </summary>
        public string FormatQuery(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return query;

            // Entferne überflüssige Leerzeichen
            query = Regex.Replace(query, @"\s+", " ").Trim();

            // Füge Zeilenumbrüche nach Keywords ein
            foreach (var keyword in Keywords)
            {
                query = Regex.Replace(
                    query,
                    $@"(?i)\b{keyword}\b",
                    match => $"\n{match.Value.ToUpper()}"
                );
            }

            // Formatiere Klammern
            query = Regex.Replace(query, @"\(", "(\n    ");
            query = Regex.Replace(query, @"\)", "\n)");

            // Formatiere Kommas
            query = Regex.Replace(query, @",", ",\n    ");

            // Entferne leere Zeilen
            query = Regex.Replace(query, @"\n\s*\n", "\n");

            // Füge Einrückung für JOIN-Bedingungen hinzu
            query = Regex.Replace(
                query,
                @"(?i)\bON\b",
                match => $"\n    {match.Value.ToUpper()}"
            );

            return query.Trim();
        }

        /// <summary>
        /// Kommentiert die ausgewählten Zeilen
        /// </summary>
        public string CommentLines(string text, int selectionStart, int selectionLength)
        {
            if (string.IsNullOrEmpty(text)) return text;

            var selectedText = text.Substring(selectionStart, selectionLength);
            var lines = selectedText.Split('\n');
            var commentedLines = lines.Select(line => $"-- {line}");
            
            return text.Substring(0, selectionStart) +
                   string.Join("\n", commentedLines) +
                   text.Substring(selectionStart + selectionLength);
        }
    }
}
