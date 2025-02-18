using Microsoft.Data.SqlClient;
using SQLServerAdmin.Models;
using System.Data;

namespace SQLServerAdmin.Services
{
    public class JobService
    {
        private readonly IConnectionService _connectionService;

        public JobService(IConnectionService connectionService)
        {
            _connectionService = connectionService;
        }

        public async Task<List<Job>> GetJobsAsync()
        {
            if (_connectionService.CurrentConnection == null)
                throw new InvalidOperationException("Keine aktive Datenbankverbindung.");

            var jobs = new List<Job>();
            var query = @"
                SELECT 
                    j.job_id,
                    j.name,
                    j.description,
                    CASE j.enabled 
                        WHEN 1 THEN 'Aktiviert' 
                        ELSE 'Deaktiviert' 
                    END AS status,
                    CONVERT(DATETIME, 
                        STUFF(STUFF(CAST(jh.run_date AS VARCHAR(8)), 7, 0, '-'), 5, 0, '-') + ' ' + 
                        STUFF(STUFF(RIGHT('000000' + CAST(jh.run_time AS VARCHAR(6)), 6), 5, 0, ':'), 3, 0, ':')
                    ) AS last_run,
                    CASE jh.run_status
                        WHEN 0 THEN 'Fehlgeschlagen'
                        WHEN 1 THEN 'Erfolgreich'
                        WHEN 2 THEN 'Wiederholen'
                        WHEN 3 THEN 'Abgebrochen'
                        WHEN 4 THEN 'In Ausführung'
                        ELSE 'Unbekannt'
                    END AS last_run_outcome,
                    CASE 
                        WHEN js.next_run_date = 0 THEN NULL
                        ELSE CONVERT(DATETIME, 
                            STUFF(STUFF(CAST(js.next_run_date AS VARCHAR(8)), 7, 0, '-'), 5, 0, '-') + ' ' + 
                            STUFF(STUFF(RIGHT('000000' + CAST(js.next_run_time AS VARCHAR(6)), 6), 5, 0, ':'), 3, 0, ':')
                        )
                    END AS next_run
                FROM msdb.dbo.sysjobs j
                LEFT JOIN (
                    SELECT job_id, run_date, run_time, run_status
                    FROM msdb.dbo.sysjobhistory
                    WHERE step_id = 0
                ) jh ON j.job_id = jh.job_id
                LEFT JOIN msdb.dbo.sysjobschedules js ON j.job_id = js.job_id
                ORDER BY j.name";

            using var command = new SqlCommand(query, _connectionService.CurrentConnection);
            using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                var lastRun = reader["last_run"] != DBNull.Value 
                    ? (DateTime)reader["last_run"] 
                    : DateTime.MinValue;
                
                var nextRun = reader["next_run"] != DBNull.Value 
                    ? (DateTime)reader["next_run"] 
                    : DateTime.MaxValue;

                jobs.Add(new Job(
                    reader["job_id"].ToString()!,
                    reader["name"].ToString()!,
                    reader["description"].ToString() ?? string.Empty,
                    reader["status"].ToString()!,
                    lastRun,
                    reader["last_run_outcome"].ToString()!,
                    nextRun
                ));
            }

            return jobs;
        }

        public async Task StartJobAsync(string jobId)
        {
            if (_connectionService.CurrentConnection == null)
                throw new InvalidOperationException("Keine aktive Datenbankverbindung.");

            var query = "EXEC msdb.dbo.sp_start_job @job_id = @JobId";
            using var command = new SqlCommand(query, _connectionService.CurrentConnection);
            command.Parameters.AddWithValue("@JobId", jobId);
            await command.ExecuteNonQueryAsync();
        }

        public async Task StopJobAsync(string jobId)
        {
            if (_connectionService.CurrentConnection == null)
                throw new InvalidOperationException("Keine aktive Datenbankverbindung.");

            var query = "EXEC msdb.dbo.sp_stop_job @job_id = @JobId";
            using var command = new SqlCommand(query, _connectionService.CurrentConnection);
            command.Parameters.AddWithValue("@JobId", jobId);
            await command.ExecuteNonQueryAsync();
        }
    }
}
