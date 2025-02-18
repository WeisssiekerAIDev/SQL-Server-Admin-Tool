using System.Data;
using System.IO;
using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;
using Serilog;
using System.Collections.ObjectModel;
using CsvHelper;
using System.Globalization;
using OfficeOpenXml;
using System.Text.Json;
using SQLServerAdmin.Models;

namespace SQLServerAdmin.Services
{
    public class QueryTemplate
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Query { get; set; } = "";
    }

    public class QueryService
    {
        private readonly string _historyFilePath;
        private readonly string _templatesFilePath;
        private readonly ConnectionService _connectionService;
        private ObservableCollection<QueryHistoryItem> _queryHistory;
        private ObservableCollection<QueryTemplate> _queryTemplates;

        public ObservableCollection<QueryHistoryItem> QueryHistory => _queryHistory;
        public ObservableCollection<QueryTemplate> QueryTemplates => _queryTemplates;

        public QueryService(ConnectionService connectionService)
        {
            _connectionService = connectionService;
            _queryHistory = new ObservableCollection<QueryHistoryItem>();
            _queryTemplates = new ObservableCollection<QueryTemplate>();
            
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SQLServerAdmin");
            
            Directory.CreateDirectory(appDataPath);
            _historyFilePath = Path.Combine(appDataPath, "query_history.json");
            _templatesFilePath = Path.Combine(appDataPath, "query_templates.json");
            
            LoadHistory();
            LoadTemplates();
        }

        private void LoadHistory()
        {
            try
            {
                if (File.Exists(_historyFilePath))
                {
                    var json = File.ReadAllText(_historyFilePath);
                    var items = JsonSerializer.Deserialize<List<QueryHistoryItem>>(json);
                    _queryHistory = new ObservableCollection<QueryHistoryItem>(items ?? new List<QueryHistoryItem>());
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Fehler beim Laden des Query-Verlaufs");
            }
        }

        private void SaveHistory()
        {
            try
            {
                var json = JsonSerializer.Serialize(_queryHistory);
                File.WriteAllText(_historyFilePath, json);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Fehler beim Speichern des Query-Verlaufs");
            }
        }

        private void LoadTemplates()
        {
            try
            {
                if (File.Exists(_templatesFilePath))
                {
                    var json = File.ReadAllText(_templatesFilePath);
                    var templates = JsonSerializer.Deserialize<List<QueryTemplate>>(json);
                    _queryTemplates = new ObservableCollection<QueryTemplate>(templates ?? GetDefaultTemplates());
                }
                else
                {
                    _queryTemplates = new ObservableCollection<QueryTemplate>(GetDefaultTemplates());
                    SaveTemplates();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Fehler beim Laden der Query-Templates");
                _queryTemplates = new ObservableCollection<QueryTemplate>(GetDefaultTemplates());
            }
        }

        private void SaveTemplates()
        {
            try
            {
                var json = JsonSerializer.Serialize(_queryTemplates);
                File.WriteAllText(_templatesFilePath, json);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Fehler beim Speichern der Query-Templates");
            }
        }

        private List<QueryTemplate> GetDefaultTemplates()
        {
            return new List<QueryTemplate>
            {
                new QueryTemplate 
                { 
                    Name = "Tabellen auflisten",
                    Description = "Listet alle Tabellen in der aktuellen Datenbank auf",
                    Query = @"SELECT 
    s.name AS Schema,
    t.name AS Table,
    p.rows AS RowCount,
    CAST(ROUND((SUM(a.total_pages) * 8) / 1024.0, 2) AS DECIMAL(18,2)) AS TotalSpaceMB
FROM sys.tables t
INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
INNER JOIN sys.indexes i ON t.object_id = i.object_id
INNER JOIN sys.partitions p ON i.object_id = p.object_id AND i.index_id = p.index_id
INNER JOIN sys.allocation_units a ON p.partition_id = a.container_id
GROUP BY s.name, t.name, p.rows
ORDER BY s.name, t.name"
                },
                new QueryTemplate
                {
                    Name = "Speicherplatz analysieren",
                    Description = "Zeigt Speicherplatzverbrauch der Datenbank",
                    Query = @"SELECT 
    DB_NAME() AS DatabaseName,
    name AS FileName,
    physical_name AS PhysicalName,
    type_desc AS FileType,
    size/128.0 AS CurrentSizeMB,
    size/128.0 - CAST(FILEPROPERTY(name, 'SpaceUsed') AS INT)/128.0 AS FreeSpaceMB
FROM sys.database_files
ORDER BY type_desc DESC"
                }
            };
        }

        public async Task<(DataTable? result, string? error)> ExecuteQueryAsync(string query, string? database = null)
        {
            var startTime = DateTime.Now;
            var historyItem = new QueryHistoryItem
            {
                Query = query,
                ExecutionTime = startTime,
                Database = database ?? "",
                Success = false
            };

            try
            {
                var connection = _connectionService.CurrentConnection;
                if (connection == null)
                {
                    throw new InvalidOperationException("Keine aktive Datenbankverbindung");
                }

                if (!string.IsNullOrEmpty(database))
                {
                    using var useDbCommand = new SqlCommand($"USE [{database}]", connection);
                    await useDbCommand.ExecuteNonQueryAsync();
                }

                using var command = new SqlCommand(query, connection);
                var adapter = new SqlDataAdapter(command);
                var table = new DataTable();
                adapter.Fill(table);

                historyItem.Success = true;
                historyItem.Duration = DateTime.Now - startTime;
                _queryHistory.Insert(0, historyItem);
                SaveHistory();

                return (table, null);
            }
            catch (Exception ex)
            {
                historyItem.Error = ex.Message;
                historyItem.Duration = DateTime.Now - startTime;
                _queryHistory.Insert(0, historyItem);
                SaveHistory();

                Log.Error(ex, "Fehler bei der Ausführung der Query: {Query}", query);
                return (null, ex.Message);
            }
        }

        public async Task<string?> GetQueryPlanAsync(string query, string? database = null)
        {
            try
            {
                var connection = _connectionService.CurrentConnection;
                if (connection == null)
                {
                    throw new InvalidOperationException("Keine aktive Datenbankverbindung");
                }

                if (!string.IsNullOrEmpty(database))
                {
                    using var useDbCommand = new SqlCommand($"USE [{database}]", connection);
                    await useDbCommand.ExecuteNonQueryAsync();
                }

                using var command = new SqlCommand(query, connection);
                command.CommandText = "SET SHOWPLAN_XML ON";
                await command.ExecuteNonQueryAsync();

                command.CommandText = query;
                var plan = (string)await command.ExecuteScalarAsync();

                command.CommandText = "SET SHOWPLAN_XML OFF";
                await command.ExecuteNonQueryAsync();

                return plan;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Fehler beim Abrufen des Ausführungsplans");
                return null;
            }
        }

        public async Task ExportToCsvAsync(DataTable data, string filePath)
        {
            try
            {
                using var writer = new StreamWriter(filePath);
                using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

                // Schreibe Header
                foreach (DataColumn column in data.Columns)
                {
                    csv.WriteField(column.ColumnName);
                }
                csv.NextRecord();

                // Schreibe Daten
                foreach (DataRow row in data.Rows)
                {
                    for (var i = 0; i < data.Columns.Count; i++)
                    {
                        csv.WriteField(row[i]?.ToString());
                    }
                    csv.NextRecord();
                }

                Log.Information("Daten erfolgreich als CSV exportiert: {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Fehler beim CSV-Export");
                throw;
            }
        }

        public async Task ExportToExcelAsync(DataTable data, string filePath)
        {
            try
            {
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                using var package = new ExcelPackage();
                var worksheet = package.Workbook.Worksheets.Add("Ergebnisse");

                // Schreibe Header
                for (int col = 0; col < data.Columns.Count; col++)
                {
                    worksheet.Cells[1, col + 1].Value = data.Columns[col].ColumnName;
                    worksheet.Cells[1, col + 1].Style.Font.Bold = true;
                }

                // Schreibe Daten
                for (int row = 0; row < data.Rows.Count; row++)
                {
                    for (int col = 0; col < data.Columns.Count; col++)
                    {
                        worksheet.Cells[row + 2, col + 1].Value = data.Rows[row][col];
                    }
                }

                // Formatiere Tabelle
                var tableRange = worksheet.Cells[1, 1, data.Rows.Count + 1, data.Columns.Count];
                var table = worksheet.Tables.Add(tableRange, "Ergebnisse");
                table.ShowFilter = true;

                worksheet.Cells.AutoFitColumns();

                await package.SaveAsAsync(new FileInfo(filePath));
                Log.Information("Daten erfolgreich als Excel exportiert: {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Fehler beim Excel-Export");
                throw;
            }
        }

        public void AddTemplate(QueryTemplate template)
        {
            _queryTemplates.Add(template);
            SaveTemplates();
        }

        public void RemoveTemplate(QueryTemplate template)
        {
            _queryTemplates.Remove(template);
            SaveTemplates();
        }

        public void ClearHistory()
        {
            _queryHistory.Clear();
            SaveHistory();
        }
    }
}
