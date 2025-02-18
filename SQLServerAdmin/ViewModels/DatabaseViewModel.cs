using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Collections.ObjectModel;
using SQLServerAdmin.Services;
using SQLServerAdmin.Models;
using SQLServerAdmin.Commands;

namespace SQLServerAdmin.ViewModels
{
    public class DatabaseViewModel : INotifyPropertyChanged
    {
        private readonly IConnectionService _connectionService;
        private ObservableCollection<Database> _databases;
        private Database? _selectedDatabase;
        private bool _isLoading;

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<Database> Databases
        {
            get => _databases;
            private set
            {
                _databases = value;
                OnPropertyChanged();
            }
        }

        public Database? SelectedDatabase
        {
            get => _selectedDatabase;
            set
            {
                _selectedDatabase = value;
                OnPropertyChanged();
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            private set
            {
                _isLoading = value;
                OnPropertyChanged();
            }
        }

        public ICommand RefreshDatabasesCommand { get; }

        public DatabaseViewModel(IConnectionService connectionService)
        {
            _connectionService = connectionService;
            _databases = new ObservableCollection<Database>();
            RefreshDatabasesCommand = new AsyncRelayCommand(LoadDatabasesAsync);
        }

        public async Task LoadDatabasesAsync()
        {
            try
            {
                IsLoading = true;
                Databases.Clear();

                var databases = await _connectionService.GetDatabasesAsync();
                
                foreach (var db in databases)
                {
                    Databases.Add(db);
                }
            }
            finally
            {
                IsLoading = false;
            }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
