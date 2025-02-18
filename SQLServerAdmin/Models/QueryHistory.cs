using System;

namespace SQLServerAdmin.Models
{
    public class QueryHistory
    {
        public string Query { get; set; }
        public DateTime ExecutionTime { get; set; }
        public string Database { get; set; }
        public int RowsAffected { get; set; }
    }
}
