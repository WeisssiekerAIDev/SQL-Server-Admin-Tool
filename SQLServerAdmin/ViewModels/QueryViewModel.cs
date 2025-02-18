using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Data;
using Microsoft.Data.SqlClient;
using SQLServerAdmin.Services;
using SQLServerAdmin.Commands;
using Serilog;

namespace SQLServerAdmin.ViewModels
{
    public class QueryViewModel : INotifyPropertyChanged
    {
        private readonly IConnectionService _connectionService;
        private readonly IQueryExecutionService _queryExecutionService;
        private string _queryText = string.Empty;
        private DataView? _queryResults;
        private string? _lastError;
        private bool _isExecuting;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string QueryText
        {
            get => _queryText;
            set
            {
                _queryText = value;
                OnPropertyChanged();
            }
        }

        public DataView? QueryResults
        {
            get => _queryResults;
            private set
            {
                _queryResults = value;
                OnPropertyChanged();
            }
        }

        public string? LastError
        {
            get => _lastError;
            private set
            {
                _lastError = value;
                OnPropertyChanged();
            }
        }

        public bool IsExecuting
        {
            get => _isExecuting;
            private set
            {
                _isExecuting = value;
                OnPropertyChanged();
            }
        }

        public ICommand ExecuteQueryCommand { get; }

        public QueryViewModel(IConnectionService connectionService, IQueryExecutionService queryExecutionService)
        {
            _connectionService = connectionService;
            _queryExecutionService = queryExecutionService;
            ExecuteQueryCommand = new AsyncRelayCommand(ExecuteQueryAsync);
        }

        private async Task ExecuteQueryAsync()
        {
            if (string.IsNullOrWhiteSpace(QueryText))
            {
                LastError = "Keine Abfrage angegeben";
                return;
            }

            try
            {
                IsExecuting = true;
                LastError = null;

                var result = await _queryExecutionService.ExecuteQueryAsync(QueryText);
                QueryResults = result?.DefaultView;

                Log.Information("Abfrage erfolgreich ausgeführt");
            }
            catch (SqlException ex)
            {
                LastError = $"SQL-Fehler: {ex.Message}";
                Log.Error(ex, "SQL-Fehler bei der Ausführung der Abfrage");
            }
            catch (Exception ex)
            {
                LastError = $"Fehler: {ex.Message}";
                Log.Error(ex, "Allgemeiner Fehler bei der Ausführung der Abfrage");
            }
            finally
            {
                IsExecuting = false;
            }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
