using System.Collections.ObjectModel;
using System.Threading.Tasks;
using SqlServerTool.Core.Models;
using SqlServerTool.Core.Services;
using SqlServerTool.Core.Commands;
using SqlServerTool.Core.ViewModels;
using System.Windows.Input;

namespace SqlServerTool.Analysis.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly DatabaseService _databaseService;
        public ObservableCollection<string> Databases { get; } = new();
        public ObservableCollection<string> PerformanceMetrics { get; } = new();
        public ObservableCollection<string> QueryStats { get; } = new();
        public ICommand AnalyzeCommand { get; }

        public MainViewModel(DatabaseService databaseService)
        {
            _databaseService = databaseService;
            AnalyzeCommand = new RelayCommand(ExecuteAnalyze);
            LoadDatabases();
        }

        private async void LoadDatabases()
        {
            var connection = new ServerConnection { ServerName = ".", IntegratedSecurity = true };
            var databases = await _databaseService.GetDatabases(connection);
            foreach (var db in databases)
            {
                Databases.Add(db);
            }
        }

        private void ExecuteAnalyze()
        {
            PerformanceMetrics.Clear();
            PerformanceMetrics.Add("CPU Usage: 45%");
            PerformanceMetrics.Add("Memory Usage: 2.5GB");
            
            QueryStats.Clear();
            QueryStats.Add("Active Queries: 5");
            QueryStats.Add("Avg Response Time: 250ms");
        }
    }
}
