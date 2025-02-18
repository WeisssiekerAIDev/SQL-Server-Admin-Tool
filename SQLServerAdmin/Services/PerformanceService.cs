using System.Data;
using Microsoft.Data.SqlClient;

namespace SQLServerAdmin.Services
{
    /// <summary>
    /// Service für Performance-Monitoring des SQL Servers
    /// </summary>
    public class PerformanceService
    {
        private readonly IConnectionService _connectionService;

        public PerformanceService(IConnectionService connectionService)
        {
            _connectionService = connectionService;
        }

        /// <summary>
        /// Holt die aktuelle CPU-Auslastung des SQL Servers
        /// </summary>
        public async Task<DataTable> GetCpuUsageAsync()
        {
            var connection = await _connectionService.GetConnectionAsync();
            var cpuQuery = @"
                SELECT 
                    SQLProcessUtilization AS 'SQL Server CPU %',
                    SystemIdle AS 'System Idle %',
                    100 - SystemIdle - SQLProcessUtilization AS 'Andere Prozesse %'
                FROM (
                    SELECT TOP 1
                        record.value('(./Record/@id)[1]', 'int') AS record_id,
                        record.value('(./Record/SchedulerMonitorEvent/SystemHealth/SystemIdle)[1]', 'int') AS SystemIdle,
                        record.value('(./Record/SchedulerMonitorEvent/SystemHealth/ProcessUtilization)[1]', 'int') AS SQLProcessUtilization
                    FROM (
                        SELECT TOP 1 CONVERT(XML, record) AS record 
                        FROM sys.dm_os_ring_buffers 
                        WHERE ring_buffer_type = N'RING_BUFFER_SCHEDULER_MONITOR'
                        AND record LIKE '%<SystemHealth>%'
                        ORDER BY timestamp DESC
                    ) AS x
                ) AS y";

            using var command = new SqlCommand(cpuQuery, connection);
            var table = new DataTable();
            using var adapter = new SqlDataAdapter(command);
            adapter.Fill(table);
            return table;
        }

        /// <summary>
        /// Holt die aktuellen Speicherauslastung des SQL Servers
        /// </summary>
        public async Task<DataTable> GetMemoryUsageAsync()
        {
            var connection = await _connectionService.GetConnectionAsync();
            var memoryQuery = @"
                SELECT
                    (physical_memory_in_use_kb/1024) AS 'Verwendeter Speicher (MB)',
                    (locked_page_allocations_kb/1024) AS 'Gesperrte Seiten (MB)',
                    (total_virtual_address_space_kb/1024) AS 'Virtueller Adressraum (MB)',
                    process_physical_memory_low AS 'Wenig Physikalischer Speicher',
                    process_virtual_memory_low AS 'Wenig Virtueller Speicher'
                FROM sys.dm_os_process_memory";

            using var command = new SqlCommand(memoryQuery, connection);
            var table = new DataTable();
            using var adapter = new SqlDataAdapter(command);
            adapter.Fill(table);
            return table;
        }

        /// <summary>
        /// Holt die aktiven Verbindungen zum SQL Server
        /// </summary>
        public async Task<DataTable> GetActiveConnectionsAsync()
        {
            var connection = await _connectionService.GetConnectionAsync();
            var connectionsQuery = @"
                SELECT 
                    DB_NAME(dbid) as 'Datenbank',
                    COUNT(dbid) as 'Anzahl Verbindungen',
                    loginame as 'Login Name',
                    hostname as 'Host Name'
                FROM sys.sysprocesses
                WHERE dbid > 0
                GROUP BY dbid, loginame, hostname
                ORDER BY 'Anzahl Verbindungen' DESC";

            using var command = new SqlCommand(connectionsQuery, connection);
            var table = new DataTable();
            using var adapter = new SqlDataAdapter(command);
            adapter.Fill(table);
            return table;
        }
    }
}
