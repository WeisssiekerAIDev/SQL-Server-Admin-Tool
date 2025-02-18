using System.Data;
using Microsoft.Data.SqlClient;
using Serilog;

namespace SQLServerAdmin.Services
{
    /// <summary>
    /// Service für die Auto-Vervollständigung
    /// </summary>
    public class IntelliSenseService
    {
        private readonly IConnectionService _connectionService;
        private Dictionary<string, List<string>> _tableCache;
        private Dictionary<string, List<string>> _columnCache;
        private DateTime _lastCacheUpdate;
        private const int CacheTimeoutMinutes = 5;

        public IntelliSenseService(IConnectionService connectionService)
        {
            _connectionService = connectionService;
            _tableCache = new Dictionary<string, List<string>>();
            _columnCache = new Dictionary<string, List<string>>();
            _lastCacheUpdate = DateTime.MinValue;
        }

        /// <summary>
        /// Holt Vorschläge für die Auto-Vervollständigung
        /// </summary>
        public async Task<List<string>> GetSuggestionsAsync(string partialWord, string database)
        {
            if (string.IsNullOrEmpty(partialWord)) return new List<string>();

            try
            {
                await UpdateCacheIfNeededAsync(database);

                var suggestions = new List<string>();

                // Tabellen durchsuchen
                if (_tableCache.ContainsKey(database))
                {
                    suggestions.AddRange(_tableCache[database]
                        .Where(t => t.StartsWith(partialWord, StringComparison.OrdinalIgnoreCase)));
                }

                // Spalten durchsuchen
                if (_columnCache.ContainsKey(database))
                {
                    suggestions.AddRange(_columnCache[database]
                        .Where(c => c.StartsWith(partialWord, StringComparison.OrdinalIgnoreCase)));
                }

                // SQL-Schlüsselwörter hinzufügen
                suggestions.AddRange(GetSqlKeywords()
                    .Where(k => k.StartsWith(partialWord, StringComparison.OrdinalIgnoreCase)));

                return suggestions.Distinct().OrderBy(s => s).ToList();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Fehler beim Abrufen der Auto-Vervollständigungs-Vorschläge");
                return new List<string>();
            }
        }

        public async Task<string> GetObjectInfoAsync(string objectName, string database)
        {
            if (string.IsNullOrEmpty(objectName)) return string.Empty;

            try
            {
                using var connection = await _connectionService.GetConnectionAsync();

                var infoQuery = @"
                    SELECT 
                        OBJECT_DEFINITION(OBJECT_ID(@objectName)) as Definition,
                        o.type_desc as ObjectType
                    FROM sys.objects o
                    WHERE o.name = @objectName
                    AND o.type IN ('U', 'V', 'P', 'FN', 'TF', 'IF')
                    AND DATABASE_ID() = DB_ID(@database)";

                using var command = new SqlCommand(infoQuery, connection);
                command.Parameters.AddWithValue("@objectName", objectName);
                command.Parameters.AddWithValue("@database", database);

                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var definition = reader.IsDBNull(0) ? "Keine Definition verfügbar" : reader.GetString(0);
                    var objectType = reader.GetString(1);
                    return $"Typ: {objectType}\n\nDefinition:\n{definition}";
                }

                return "Keine Information verfügbar";
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Fehler beim Abrufen der Objektinformation");
                return $"Fehler: {ex.Message}";
            }
        }

        private async Task UpdateCacheIfNeededAsync(string database)
        {
            if (!_tableCache.ContainsKey(database) || 
                DateTime.Now - _lastCacheUpdate > TimeSpan.FromMinutes(CacheTimeoutMinutes))
            {
                await RefreshCacheAsync(database);
            }
        }

        private async Task RefreshCacheAsync(string database)
        {
            try
            {
                var connection = await _connectionService.GetConnectionAsync();

                // Tabellen abrufen
                var tableQuery = @"
                    SELECT TABLE_NAME 
                    FROM INFORMATION_SCHEMA.TABLES 
                    WHERE TABLE_TYPE = 'BASE TABLE'";

                using (var command = new SqlCommand(tableQuery, connection))
                {
                    var tables = new List<string>();
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            tables.Add(reader.GetString(0));
                        }
                    }
                    _tableCache[database] = tables;
                }

                // Spalten abrufen
                var columnQuery = @"
                    SELECT DISTINCT COLUMN_NAME 
                    FROM INFORMATION_SCHEMA.COLUMNS";

                using (var command = new SqlCommand(columnQuery, connection))
                {
                    var columns = new List<string>();
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            columns.Add(reader.GetString(0));
                        }
                    }
                    _columnCache[database] = columns;
                }

                _lastCacheUpdate = DateTime.Now;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Fehler beim Aktualisieren des IntelliSense-Caches");
                _tableCache[database] = new List<string>();
                _columnCache[database] = new List<string>();
            }
        }

        private IEnumerable<string> GetSqlKeywords()
        {
            return new[]
            {
                "SELECT", "FROM", "WHERE", "INSERT", "UPDATE", "DELETE",
                "JOIN", "INNER JOIN", "LEFT JOIN", "RIGHT JOIN",
                "GROUP BY", "ORDER BY", "HAVING",
                "AND", "OR", "NOT", "IN", "BETWEEN", "LIKE",
                "COUNT", "SUM", "AVG", "MIN", "MAX",
                "CREATE", "ALTER", "DROP", "TRUNCATE",
                "TABLE", "VIEW", "INDEX", "PROCEDURE",
                "DISTINCT", "TOP", "AS", "WITH"
            };
        }
    }
}
