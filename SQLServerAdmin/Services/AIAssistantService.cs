using Azure;
using Azure.AI.OpenAI;
using Microsoft.Data.SqlClient;
using System.Text;
using System.Text.Json;

namespace SQLServerAdmin.Services
{
    public class AIAssistantService
    {
        private readonly OpenAIClient _client;
        private readonly IConnectionService _connectionService;
        private const string MODEL_NAME = "gpt-4-turbo-preview";

        public AIAssistantService(string apiKey, IConnectionService connectionService)
        {
            _client = new OpenAIClient(apiKey);
            _connectionService = connectionService;
        }

        public async Task<string> OptimizeQueryAsync(string query)
        {
            var messages = new List<ChatMessage>
            {
                new ChatMessage(ChatRole.System, "Du bist ein SQL-Experte. Analysiere und optimiere die gegebene SQL-Query."),
                new ChatMessage(ChatRole.User, query)
            };

            var response = await _client.GetChatCompletionsAsync(
                MODEL_NAME,
                new ChatCompletionsOptions(messages));

            return response.Value.Choices[0].Message.Content;
        }

        public async Task<string> GenerateDocumentationAsync(string storedProcedure)
        {
            var schema = await GetStoredProcedureSchemaAsync(storedProcedure);
            
            var messages = new List<ChatMessage>
            {
                new ChatMessage(ChatRole.System, "Erstelle eine detaillierte Dokumentation für diese Stored Procedure. " +
                    "Beschreibe den Zweck, die Parameter, die Rückgabewerte und gib Beispiele."),
                new ChatMessage(ChatRole.User, schema)
            };

            var response = await _client.GetChatCompletionsAsync(
                MODEL_NAME,
                new ChatCompletionsOptions(messages));

            return response.Value.Choices[0].Message.Content;
        }

        public async Task<string> GenerateQueryFromNaturalLanguageAsync(string naturalLanguageQuery, string databaseContext)
        {
            var schemaInfo = await GetDatabaseSchemaInfoAsync(databaseContext);
            
            var messages = new List<ChatMessage>
            {
                new ChatMessage(ChatRole.System, 
                    $"Du bist ein SQL-Experte. Erstelle eine SQL-Query basierend auf der natürlichsprachlichen Anfrage. " +
                    $"Nutze das folgende Datenbankschema: {schemaInfo}"),
                new ChatMessage(ChatRole.User, naturalLanguageQuery)
            };

            var response = await _client.GetChatCompletionsAsync(
                MODEL_NAME,
                new ChatCompletionsOptions(messages));

            return response.Value.Choices[0].Message.Content;
        }

        public async Task<string> ReviewQueryAsync(string query)
        {
            var messages = new List<ChatMessage>
            {
                new ChatMessage(ChatRole.System, 
                    "Führe ein Code-Review der SQL-Query durch. Prüfe auf:" +
                    "\n- Potenzielle Sicherheitsprobleme (SQL Injection, etc.)" +
                    "\n- Performance-Probleme" +
                    "\n- Best Practices" +
                    "\n- Wartbarkeit"),
                new ChatMessage(ChatRole.User, query)
            };

            var response = await _client.GetChatCompletionsAsync(
                MODEL_NAME,
                new ChatCompletionsOptions(messages));

            return response.Value.Choices[0].Message.Content;
        }

        public async Task<string> AnalyzeIndexesAsync(string tableName)
        {
            var indexInfo = await GetTableIndexInfoAsync(tableName);
            var queryPatterns = await GetTableQueryPatternsAsync(tableName);
            
            var messages = new List<ChatMessage>
            {
                new ChatMessage(ChatRole.System, 
                    "Analysiere die Indexstruktur der Tabelle und gib Empfehlungen. Berücksichtige:" +
                    "\n- Aktuelle Indizes und ihre Nutzung" +
                    "\n- Häufige Abfragemuster" +
                    "\n- Fehlende Indizes" +
                    "\n- Überflüssige Indizes" +
                    "\n- Fragmentierung"),
                new ChatMessage(ChatRole.User, 
                    $"Tabellenindizes:\n{indexInfo}\n\nAbfragemuster:\n{queryPatterns}")
            };

            var response = await _client.GetChatCompletionsAsync(
                MODEL_NAME,
                new ChatCompletionsOptions(messages));

            return response.Value.Choices[0].Message.Content;
        }

        public async Task<string> AnalyzePerformanceAsync(string query)
        {
            var executionPlan = await GetQueryExecutionPlanAsync(query);
            var statistics = await GetQueryStatisticsAsync(query);
            
            var messages = new List<ChatMessage>
            {
                new ChatMessage(ChatRole.System, 
                    "Analysiere den Ausführungsplan und die Statistiken der Query. Gib detaillierte Empfehlungen für:" +
                    "\n- Performanceoptimierung" +
                    "\n- Ressourcennutzung" +
                    "\n- Potenzielle Bottlenecks" +
                    "\n- Caching-Strategien"),
                new ChatMessage(ChatRole.User, 
                    $"Ausführungsplan:\n{executionPlan}\n\nStatistiken:\n{statistics}")
            };

            var response = await _client.GetChatCompletionsAsync(
                MODEL_NAME,
                new ChatCompletionsOptions(messages));

            return response.Value.Choices[0].Message.Content;
        }

        public async Task<string> SuggestErrorFixAsync(string errorMessage, string query)
        {
            var messages = new List<ChatMessage>
            {
                new ChatMessage(ChatRole.System, 
                    "Analysiere die Fehlermeldung und die Query. Gib konkrete Vorschläge zur Behebung des Problems." +
                    "\nBerücksichtige häufige SQL-Fehlerquellen und Best Practices."),
                new ChatMessage(ChatRole.User, 
                    $"Fehlermeldung:\n{errorMessage}\n\nQuery:\n{query}")
            };

            var response = await _client.GetChatCompletionsAsync(
                MODEL_NAME,
                new ChatCompletionsOptions(messages));

            return response.Value.Choices[0].Message.Content;
        }

        private async Task<string> GetStoredProcedureSchemaAsync(string storedProcedure)
        {
            using var connection = await _connectionService.GetConnectionAsync();
            using var command = new SqlCommand(
                "SELECT OBJECT_DEFINITION(OBJECT_ID(@procName))",
                connection);
            command.Parameters.AddWithValue("@procName", storedProcedure);
            
            var definition = (string)await command.ExecuteScalarAsync();
            return definition ?? string.Empty;
        }

        private async Task<string> GetDatabaseSchemaInfoAsync(string database)
        {
            var schema = new StringBuilder();
            
            using var connection = await _connectionService.GetConnectionAsync();
            using var command = new SqlCommand(@"
                SELECT 
                    t.name AS TableName,
                    c.name AS ColumnName,
                    ty.name AS DataType,
                    c.max_length,
                    c.is_nullable
                FROM sys.tables t
                JOIN sys.columns c ON t.object_id = c.object_id
                JOIN sys.types ty ON c.system_type_id = ty.system_type_id
                WHERE t.type = 'U'
                ORDER BY t.name, c.column_id", connection);

            using var reader = await command.ExecuteReaderAsync();
            string currentTable = "";
            
            while (await reader.ReadAsync())
            {
                var tableName = reader.GetString(0);
                if (tableName != currentTable)
                {
                    schema.AppendLine($"\nTable: {tableName}");
                    currentTable = tableName;
                }
                
                schema.AppendLine($"  - {reader.GetString(1)} ({reader.GetString(2)})");
            }

            return schema.ToString();
        }

        private async Task<string> GetTableIndexInfoAsync(string tableName)
        {
            using var connection = await _connectionService.GetConnectionAsync();
            using var command = new SqlCommand(@"
                SELECT 
                    i.name AS IndexName,
                    i.type_desc AS IndexType,
                    STUFF((
                        SELECT ', ' + c.name
                        FROM sys.index_columns ic
                        JOIN sys.columns c ON ic.object_id = c.object_id 
                            AND ic.column_id = c.column_id
                        WHERE ic.object_id = i.object_id 
                            AND ic.index_id = i.index_id
                        ORDER BY ic.key_ordinal
                        FOR XML PATH('')
                    ), 1, 2, '') AS IndexColumns,
                    ps.avg_fragmentation_in_percent AS Fragmentation,
                    us.user_seeks + us.user_scans + us.user_lookups AS TotalUsage
                FROM sys.indexes i
                JOIN sys.objects o ON i.object_id = o.object_id
                LEFT JOIN sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, 'LIMITED') ps 
                    ON ps.object_id = i.object_id AND ps.index_id = i.index_id
                LEFT JOIN sys.dm_db_index_usage_stats us
                    ON us.object_id = i.object_id AND us.index_id = i.index_id
                WHERE o.name = @tableName", connection);
            
            command.Parameters.AddWithValue("@tableName", tableName);
            
            var result = new StringBuilder();
            using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                result.AppendLine($"Index: {reader.GetString(0)}");
                result.AppendLine($"  Typ: {reader.GetString(1)}");
                result.AppendLine($"  Spalten: {reader.GetString(2)}");
                result.AppendLine($"  Fragmentierung: {reader.GetDouble(3):F2}%");
                result.AppendLine($"  Nutzung: {reader.GetInt64(4)}");
                result.AppendLine();
            }
            
            return result.ToString();
        }

        private async Task<string> GetTableQueryPatternsAsync(string tableName)
        {
            using var connection = await _connectionService.GetConnectionAsync();
            using var command = new SqlCommand(@"
                SELECT TOP 10
                    qs.execution_count,
                    SUBSTRING(qt.text, (qs.statement_start_offset/2)+1,
                        ((CASE qs.statement_end_offset
                            WHEN -1 THEN DATALENGTH(qt.text)
                            ELSE qs.statement_end_offset
                        END - qs.statement_start_offset)/2) + 1) AS query_text,
                    qs.total_logical_reads/qs.execution_count AS avg_logical_reads,
                    qs.total_worker_time/qs.execution_count AS avg_cpu_time
                FROM sys.dm_exec_query_stats qs
                CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) qt
                WHERE qt.text LIKE '%' + @tableName + '%'
                ORDER BY qs.execution_count DESC", connection);
            
            command.Parameters.AddWithValue("@tableName", tableName);
            
            var result = new StringBuilder();
            using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                result.AppendLine($"Ausführungen: {reader.GetInt64(0)}");
                result.AppendLine($"Query: {reader.GetString(1)}");
                result.AppendLine($"Durchschnittliche Lesezugriffe: {reader.GetInt64(2)}");
                result.AppendLine($"Durchschnittliche CPU-Zeit: {reader.GetInt64(3)}µs");
                result.AppendLine();
            }
            
            return result.ToString();
        }

        private async Task<string> GetQueryExecutionPlanAsync(string query)
        {
            using var connection = await _connectionService.GetConnectionAsync();
            using var command = new SqlCommand(
                "SET SHOWPLAN_XML ON; " + query, connection);
            
            try
            {
                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return reader.GetString(0);
                }
            }
            finally
            {
                using var resetCommand = new SqlCommand(
                    "SET SHOWPLAN_XML OFF;", connection);
                await resetCommand.ExecuteNonQueryAsync();
            }
            
            return string.Empty;
        }

        private async Task<string> GetQueryStatisticsAsync(string query)
        {
            using var connection = await _connectionService.GetConnectionAsync();
            using var command = new SqlCommand(
                "SET STATISTICS XML ON; " + query, connection);
            
            try
            {
                using var reader = await command.ExecuteReaderAsync();
                var result = new StringBuilder();
                
                while (await reader.ReadAsync())
                {
                    if (reader.GetName(0) == "Microsoft SQL Server 2005 XML Showplan")
                    {
                        result.AppendLine(reader.GetString(0));
                    }
                }
                
                return result.ToString();
            }
            finally
            {
                using var resetCommand = new SqlCommand(
                    "SET STATISTICS XML OFF;", connection);
                await resetCommand.ExecuteNonQueryAsync();
            }
        }
    }
}
