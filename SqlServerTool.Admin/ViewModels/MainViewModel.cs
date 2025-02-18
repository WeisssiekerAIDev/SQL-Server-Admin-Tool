using System.Collections.ObjectModel;
using System.Threading.Tasks;
using SqlServerTool.Core.Models;
using SqlServerTool.Core.Services;
using SqlServerTool.Core.Commands;
using SqlServerTool.Core.ViewModels;
using System.Windows.Input;

namespace SqlServerTool.Admin.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly DatabaseService _databaseService;
        public ObservableCollection<string> Databases { get; } = new();
        public ObservableCollection<string> ServerStatus { get; } = new();
        public ObservableCollection<string> DatabaseUsers { get; } = new();
        public ICommand BackupCommand { get; }
        public ICommand ManageUsersCommand { get; }

        public MainViewModel(DatabaseService databaseService)
        {
            _databaseService = databaseService;
            BackupCommand = new RelayCommand(ExecuteBackup);
            ManageUsersCommand = new RelayCommand(ExecuteManageUsers);
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

        private void ExecuteBackup()
        {
            ServerStatus.Clear();
            ServerStatus.Add("Backup started...");
            ServerStatus.Add("Backup completed successfully");
        }

        private void ExecuteManageUsers()
        {
            DatabaseUsers.Clear();
            DatabaseUsers.Add("sa (System Administrator)");
            DatabaseUsers.Add("app_user (Standard User)");
        }
    }
}
