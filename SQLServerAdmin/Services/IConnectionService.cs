using Microsoft.Data.SqlClient;
using SQLServerAdmin.Models;

namespace SQLServerAdmin.Services
{
    public interface IConnectionService
    {
        string ConnectionString { get; }
        Task<SqlConnection> GetConnectionAsync();
        Task<IEnumerable<Database>> GetDatabasesAsync();
        void UpdateConnectionString(string server, string? database = null, string? username = null, string? password = null);
    }
}
