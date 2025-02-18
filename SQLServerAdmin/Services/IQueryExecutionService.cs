using System.Data;

namespace SQLServerAdmin.Services
{
    public interface IQueryExecutionService
    {
        Task<DataTable?> ExecuteQueryAsync(string query);
        Task<int> ExecuteNonQueryAsync(string query);
        Task<object?> ExecuteScalarAsync(string query);
    }
}
