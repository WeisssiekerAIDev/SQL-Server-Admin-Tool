using System.Data;

namespace SQLServerAdmin.Models
{
    public class QueryResult
    {
        public DataView Rows { get; set; }
        public int RowsAffected { get; set; }

        public QueryResult(DataTable data, int rowsAffected = 0)
        {
            Rows = data.DefaultView;
            RowsAffected = rowsAffected;
        }
    }
}
