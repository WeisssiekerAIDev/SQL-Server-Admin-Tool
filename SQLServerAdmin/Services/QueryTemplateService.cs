using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json;
using Serilog;
using SQLServerAdmin.Models;

namespace SQLServerAdmin.Services
{
    public class QueryTemplateService
    {
        private readonly string _templatePath;
        private List<SQLServerAdmin.Models.QueryTemplate> _templates;

        public QueryTemplateService()
        {
            _templatePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SQLServerAdmin",
                "templates.json"
            );
            _templates = new List<SQLServerAdmin.Models.QueryTemplate>();
            
            // Standardtemplates erstellen, wenn keine existieren
            if (!File.Exists(_templatePath))
            {
                _templates = CreateDefaultTemplates();
                SaveTemplatesAsync().Wait();
            }
        }

        public async Task<IEnumerable<SQLServerAdmin.Models.QueryTemplate>> GetTemplatesAsync()
        {
            try
            {
                if (File.Exists(_templatePath))
                {
                    var json = await File.ReadAllTextAsync(_templatePath);
                    _templates = JsonSerializer.Deserialize<List<SQLServerAdmin.Models.QueryTemplate>>(json) ?? new List<SQLServerAdmin.Models.QueryTemplate>();
                }
                return _templates;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Fehler beim Laden der Templates");
                return new List<SQLServerAdmin.Models.QueryTemplate>();
            }
        }

        public async Task SaveTemplateAsync(SQLServerAdmin.Models.QueryTemplate template)
        {
            _templates.Add(template);
            await SaveTemplatesAsync();
        }

        private async Task SaveTemplatesAsync()
        {
            try
            {
                var directory = Path.GetDirectoryName(_templatePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(_templates, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_templatePath, json);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Fehler beim Speichern der Templates");
            }
        }

        private List<SQLServerAdmin.Models.QueryTemplate> CreateDefaultTemplates()
        {
            return new List<SQLServerAdmin.Models.QueryTemplate>
            {
                new SQLServerAdmin.Models.QueryTemplate
                {
                    Name = "Datenbank-Größe",
                    Description = "Zeigt die Größe aller Datenbanken",
                    Category = "System",
                    Query = @"
SELECT 
    DB_NAME(database_id) AS DatabaseName,
    Name AS LogicalName,
    Physical_Name AS PhysicalName,
    (size*8)/1024 AS SizeMB
FROM sys.master_files
ORDER BY DB_NAME(database_id)"
                },
                new SQLServerAdmin.Models.QueryTemplate
                {
                    Name = "Aktive Verbindungen",
                    Description = "Zeigt alle aktiven Datenbankverbindungen",
                    Category = "Performance",
                    Query = @"
SELECT 
    DB_NAME(dbid) as DatabaseName,
    COUNT(dbid) as NumberOfConnections,
    loginame as LoginName
FROM sys.sysprocesses
WHERE dbid > 0
GROUP BY dbid, loginame"
                },
                new SQLServerAdmin.Models.QueryTemplate
                {
                    Name = "Index-Fragmentierung",
                    Description = "Zeigt fragmentierte Indizes",
                    Category = "Wartung",
                    Query = @"
SELECT 
    OBJECT_NAME(ind.OBJECT_ID) AS TableName,
    ind.name AS IndexName,
    indexstats.avg_fragmentation_in_percent
FROM sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, NULL) indexstats
INNER JOIN sys.indexes ind 
ON ind.object_id = indexstats.object_id
AND ind.index_id = indexstats.index_id
WHERE indexstats.avg_fragmentation_in_percent > 30
ORDER BY indexstats.avg_fragmentation_in_percent DESC"
                }
            };
        }
    }
}
