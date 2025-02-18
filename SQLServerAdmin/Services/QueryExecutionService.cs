using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;
using Serilog;
using System.Threading.Tasks;
using SQLServerAdmin.Models;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace SQLServerAdmin.Services
{
    /// <summary>
    /// Service für die Ausführung von Queries
    /// </summary>
    public class QueryExecutionService : IQueryExecutionService, IDisposable
    {
        private readonly IConnectionService _connectionService;
        private readonly QueryHistoryService _historyService;
        private bool _disposed;
        private readonly object _lockObject = new object();

        public string? CurrentDatabase { get; private set; }

        public QueryExecutionService(
            IConnectionService connectionService,
            QueryHistoryService historyService)
        {
            _connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));
            _historyService = historyService ?? throw new ArgumentNullException(nameof(historyService));
        }

        public async Task<DataTable?> ExecuteQueryAsync(string query, string? database = null)
        {
            if (string.IsNullOrEmpty(query))
            {
                throw new ArgumentException("Query darf nicht leer sein.", nameof(query));
            }

            var startTime = DateTime.Now;
            var historyItem = new QueryHistoryItem
            {
                Query = query,
                ExecutionTime = startTime,
                Database = database ?? CurrentDatabase ?? "",
                Success = false
            };

            try
            {
                var connection = await GetConnectionAsync();
                
                if (!string.IsNullOrEmpty(database))
                {
                    // Datenbank wechseln
                    using var useDbCommand = new SqlCommand($"USE [{database}]", connection);
                    await useDbCommand.ExecuteNonQueryAsync();
                    CurrentDatabase = database;
                }

                using var command = new SqlCommand(query, connection);
                command.CommandTimeout = 300; // 5 Minuten Timeout

                // Query validieren
                if (!ValidateQuery(query))
                {
                    throw new InvalidOperationException("Die Query enthält ungültige oder gefährliche Anweisungen.");
                }

                using var adapter = new SqlDataAdapter(command);
                
                var table = new DataTable();
                adapter.Fill(table);
                
                historyItem.Success = true;
                historyItem.RowsAffected = table.Rows.Count;
                historyItem.ExecutionTimeMs = (DateTime.Now - startTime).TotalMilliseconds;

                await _historyService.AddQueryAsync(historyItem);

                Log.Information(
                    "Query erfolgreich ausgeführt. {RowCount} Zeilen in {ExecutionTime:N0}ms",
                    table.Rows.Count,
                    (DateTime.Now - startTime).TotalMilliseconds);

                return table;
            }
            catch (SqlException ex)
            {
                historyItem.ErrorMessage = ex.Message;
                await _historyService.AddQueryAsync(historyItem);

                Log.Error(ex, "SQL-Fehler bei der Query-Ausführung");
                throw;
            }
            catch (Exception ex)
            {
                historyItem.ErrorMessage = ex.Message;
                await _historyService.AddQueryAsync(historyItem);

                Log.Error(ex, "Unerwarteter Fehler bei der Query-Ausführung");
                throw;
            }
        }

        public async Task<int> ExecuteNonQueryAsync(string query)
        {
            try
            {
                var connection = await GetConnectionAsync();
                using var command = new SqlCommand(query, connection);
                return await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Fehler bei der Ausführung der Non-Query: {Query}", query);
                throw;
            }
        }

        public async Task<object?> ExecuteScalarAsync(string query)
        {
            try
            {
                var connection = await GetConnectionAsync();
                using var command = new SqlCommand(query, connection);
                return await command.ExecuteScalarAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Fehler bei der Ausführung des Skalars: {Query}", query);
                throw;
            }
        }

        private async Task<SqlConnection> GetConnectionAsync()
        {
            if (_connectionService == null)
            {
                throw new InvalidOperationException("ConnectionService wurde nicht initialisiert.");
            }

            var connection = await _connectionService.GetConnectionAsync();
            if (connection == null)
            {
                throw new InvalidOperationException("Keine aktive Datenbankverbindung vorhanden.");
            }

            return connection;
        }

        private bool ValidateQuery(string query)
        {
            try
            {
                var parser = new TSql150Parser(true);
                using var reader = new StringReader(query);
                var result = parser.Parse(reader, out var errors);

                if (errors != null && errors.Count > 0)
                {
                    Log.Warning("SQL-Parser-Fehler in Query: {Errors}", string.Join(", ", errors));
                    return false;
                }

                // Prüfen auf gefährliche Befehle
                var lowerQuery = query.ToLowerInvariant();
                var dangerousCommands = new[]
                {
                    "drop database",
                    "drop login",
                    "drop user",
                    "xp_cmdshell",
                    "sp_configure"
                };

                foreach (var command in dangerousCommands)
                {
                    if (lowerQuery.Contains(command))
                    {
                        Log.Warning("Gefährlicher Befehl in Query gefunden: {Command}", command);
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Fehler bei der Query-Validierung");
                return false;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Hier können disposable Ressourcen freigegeben werden
                }
                _disposed = true;
            }
        }

        ~QueryExecutionService()
        {
            Dispose(false);
        }
    }

    public class QueryResult
    {
        public DataTable Data { get; set; } = new();
        public int RowsAffected { get; set; }
        public double ExecutionTime { get; set; }
        public bool Success { get; set; }
    }
}
