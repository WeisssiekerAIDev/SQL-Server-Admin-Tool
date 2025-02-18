namespace SQLServerAdmin.Models
{
    /// <summary>
    /// Repräsentiert einen Eintrag im Query-Verlauf
    /// </summary>
    public class QueryHistoryItem
    {
        public string Query { get; set; } = "";
        public DateTime ExecutionTime { get; set; }
        public string Database { get; set; } = "";
        public TimeSpan Duration { get; set; }
        public bool Success { get; set; }
        public string? Error { get; set; }
        public int RowsAffected { get; set; }
    }

    /// <summary>
    /// Repräsentiert ein Query-Template
    /// </summary>
    public class QueryTemplate
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Query { get; set; } = "";
        public string Category { get; set; } = "";
    }
}
