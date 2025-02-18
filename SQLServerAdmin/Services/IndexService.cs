using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;

namespace SQLServerAdmin.Services
{
    /// <summary>
    /// Service für das Management von Datenbankindizes
    /// </summary>
    public class IndexService
    {
        private readonly IConnectionService _connectionService;

        public IndexService(IConnectionService connectionService)
        {
            _connectionService = connectionService;
        }

        /// <summary>
        /// Analysiert die Indexfragmentierung für eine bestimmte Datenbank
        /// </summary>
        public async Task<DataTable> AnalyzeIndexFragmentationAsync(string databaseName)
        {
            var connection = await _connectionService.GetConnectionAsync();
            
            var fragmentationQuery = $@"
                USE [{databaseName}];
                SELECT 
                    OBJECT_SCHEMA_NAME(ips.object_id) AS 'Schema',
                    OBJECT_NAME(ips.object_id) AS 'Tabelle',
                    i.name AS 'Index',
                    ips.avg_fragmentation_in_percent AS 'Fragmentierung %',
                    ips.page_count AS 'Seiten',
                    ips.index_type_desc AS 'Index Typ',
                    CASE
                        WHEN ips.avg_fragmentation_in_percent > 30 THEN 'Rebuild empfohlen'
                        WHEN ips.avg_fragmentation_in_percent > 10 THEN 'Reorganize empfohlen'
                        ELSE 'OK'
                    END AS 'Empfehlung'
                FROM sys.dm_db_index_physical_stats(
                    DB_ID(), NULL, NULL, NULL, 'LIMITED') ips
                JOIN sys.indexes i ON ips.object_id = i.object_id 
                    AND ips.index_id = i.index_id
                WHERE ips.page_count > 1000
                ORDER BY ips.avg_fragmentation_in_percent DESC";

            using var command = new SqlCommand(fragmentationQuery, connection);
            var adapter = new SqlDataAdapter(command);
            var table = new DataTable();
            await Task.Run(() => adapter.Fill(table));
            return table;
        }

        /// <summary>
        /// Führt eine Indexwartung durch (Rebuild oder Reorganize)
        /// </summary>
        public async Task MaintenanceIndexAsync(string databaseName, string schemaName, string tableName, string indexName, bool rebuild)
        {
            var connection = await _connectionService.GetConnectionAsync();
            var serverConnection = new ServerConnection(connection);
            var server = new Server(serverConnection);
            var database = server.Databases[databaseName];
            var table = database.Tables[tableName, schemaName];
            var index = table.Indexes[indexName];

            if (rebuild)
            {
                await Task.Run(() => index.Rebuild());
            }
            else
            {
                await Task.Run(() => index.Reorganize());
            }
        }
    }
}
