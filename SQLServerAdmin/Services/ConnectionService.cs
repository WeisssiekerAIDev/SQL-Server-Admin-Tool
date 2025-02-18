using Microsoft.Data.SqlClient;
using Serilog;
using System.Security;
using System.Data;
using SQLServerAdmin.Models;

namespace SQLServerAdmin.Services
{
    public class ConnectionService : IConnectionService, IDisposable
    {
        private SqlConnection? _currentConnection;
        private bool _disposed;

        public SqlConnection? CurrentConnection => _currentConnection;

        public async Task<SqlConnection> GetConnectionAsync()
        {
            if (_currentConnection == null)
            {
                throw new InvalidOperationException("Keine aktive Datenbankverbindung. Bitte zuerst ConnectAsync aufrufen.");
            }

            if (_currentConnection.State != System.Data.ConnectionState.Open)
            {
                try
                {
                    await _currentConnection.OpenAsync();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Fehler beim Wiederherstellen der Datenbankverbindung");
                    throw;
                }
            }

            return _currentConnection;
        }

        public async Task<bool> ConnectAsync(string server, string? database = null, bool integratedSecurity = true)
        {
            try
            {
                Disconnect();

                var connectionStringBuilder = new SqlConnectionStringBuilder
                {
                    DataSource = server,
                    IntegratedSecurity = integratedSecurity,
                    TrustServerCertificate = true,
                    ApplicationName = "SQL Server Admin Tool"
                };

                if (!string.IsNullOrEmpty(database))
                {
                    connectionStringBuilder.InitialCatalog = database;
                }

                _currentConnection = new SqlConnection(connectionStringBuilder.ConnectionString);
                await _currentConnection.OpenAsync();

                Log.Information("Verbindung zu {Server} erfolgreich hergestellt", server);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Fehler beim Verbindungsaufbau zu {Server}", server);
                _currentConnection = null;
                throw;
            }
        }

        public void Disconnect()
        {
            if (_currentConnection != null)
            {
                try
                {
                    if (_currentConnection.State == System.Data.ConnectionState.Open)
                    {
                        _currentConnection.Close();
                    }
                    _currentConnection.Dispose();
                    _currentConnection = null;
                    Log.Information("Datenbankverbindung wurde getrennt");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Fehler beim Trennen der Datenbankverbindung");
                }
            }
        }

        public async Task<IEnumerable<Database>> GetDatabasesAsync()
        {
            var databases = new List<Database>();
            
            using var connection = await GetConnectionAsync();
            var query = @"
                SELECT 
                    d.name,
                    d.create_date,
                    d.compatibility_level,
                    d.collation_name,
                    CAST(SUM(mf.size) * 8.0 / 1024 AS DECIMAL(10,2)) AS size_mb
                FROM sys.databases d
                JOIN sys.master_files mf ON d.database_id = mf.database_id
                WHERE d.database_id > 4
                GROUP BY d.name, d.create_date, d.compatibility_level, d.collation_name
                ORDER BY d.name";

            using var command = new SqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                databases.Add(new Database(
                    reader.GetString(0),
                    reader.GetDateTime(1),
                    reader.GetInt32(2),
                    reader.GetString(3),
                    reader.GetDecimal(4)
                ));
            }

            return databases;
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
                    Disconnect();
                }
                _disposed = true;
            }
        }

        ~ConnectionService()
        {
            Dispose(false);
        }
    }
}
