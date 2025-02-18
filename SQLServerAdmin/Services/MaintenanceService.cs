using System.Data;
using Microsoft.Data.SqlClient;

namespace SQLServerAdmin.Services
{
    /// <summary>
    /// Service für Wartungsaufgaben des SQL Servers
    /// </summary>
    public class MaintenanceService
    {
        private readonly IConnectionService _connectionService;

        public MaintenanceService(IConnectionService connectionService)
        {
            _connectionService = connectionService;
        }

        /// <summary>
        /// Führt eine Datenbanksicherung durch
        /// </summary>
        public async Task BackupDatabaseAsync(string databaseName, string backupPath)
        {
            var connection = await _connectionService.GetConnectionAsync();
            var backupQuery = $@"
                BACKUP DATABASE [{databaseName}] 
                TO DISK = N'{backupPath}' 
                WITH NOFORMAT, 
                    NOINIT,  
                    NAME = N'{databaseName}-Full Database Backup', 
                    SKIP, 
                    NOREWIND, 
                    NOUNLOAD,  
                    STATS = 10";

            using var command = new SqlCommand(backupQuery, connection);
            await command.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Führt eine Datenbankintegritätsprüfung durch
        /// </summary>
        public async Task CheckDatabaseIntegrityAsync(string databaseName)
        {
            var connection = await _connectionService.GetConnectionAsync();
            var checkQuery = $@"
                USE [{databaseName}];
                DBCC CHECKDB WITH NO_INFOMSGS, ALL_ERRORMSGS";

            using var command = new SqlCommand(checkQuery, connection);
            await command.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Aktualisiert die Statistiken einer Datenbank
        /// </summary>
        public async Task UpdateStatisticsAsync(string databaseName)
        {
            var connection = await _connectionService.GetConnectionAsync();
            var updateStatsQuery = $@"
                USE [{databaseName}];
                EXEC sp_updatestats";

            using var command = new SqlCommand(updateStatsQuery, connection);
            await command.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Verkleinert eine Datenbank
        /// </summary>
        public async Task ShrinkDatabaseAsync(string databaseName)
        {
            var connection = await _connectionService.GetConnectionAsync();
            var shrinkQuery = $@"
                USE [{databaseName}];
                DBCC SHRINKDATABASE(N'{databaseName}')";

            using var command = new SqlCommand(shrinkQuery, connection);
            await command.ExecuteNonQueryAsync();
        }
    }
}
