using System.Collections.ObjectModel;
using System.Text.Json;
using System.IO;
using SQLServerAdmin.Models;
using Serilog;

namespace SQLServerAdmin.Services
{
    /// <summary>
    /// Service für die Verwaltung des Query-Verlaufs
    /// </summary>
    public class QueryHistoryService
    {
        private readonly string _historyFilePath;
        private ObservableCollection<QueryHistoryItem> _queryHistory;

        public ObservableCollection<QueryHistoryItem> QueryHistory => _queryHistory;

        public QueryHistoryService()
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SQLServerAdmin");
            
            Directory.CreateDirectory(appDataPath);
            _historyFilePath = Path.Combine(appDataPath, "query_history.json");
            
            LoadHistory();
        }

        /// <summary>
        /// Lädt den Query-Verlauf aus der Datei
        /// </summary>
        private void LoadHistory()
        {
            try
            {
                if (File.Exists(_historyFilePath))
                {
                    var json = File.ReadAllText(_historyFilePath);
                    var items = JsonSerializer.Deserialize<List<QueryHistoryItem>>(json);
                    _queryHistory = new ObservableCollection<QueryHistoryItem>(items ?? new List<QueryHistoryItem>());
                }
                else
                {
                    _queryHistory = new ObservableCollection<QueryHistoryItem>();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Fehler beim Laden des Query-Verlaufs");
                _queryHistory = new ObservableCollection<QueryHistoryItem>();
            }
        }

        /// <summary>
        /// Speichert den Query-Verlauf in der Datei
        /// </summary>
        private void SaveHistory()
        {
            try
            {
                var json = JsonSerializer.Serialize(_queryHistory);
                File.WriteAllText(_historyFilePath, json);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Fehler beim Speichern des Query-Verlaufs");
            }
        }

        /// <summary>
        /// Fügt einen neuen Eintrag zum Query-Verlauf hinzu
        /// </summary>
        public void AddHistoryItem(QueryHistoryItem item)
        {
            _queryHistory.Insert(0, item);
            SaveHistory();
        }

        /// <summary>
        /// Löscht den gesamten Query-Verlauf
        /// </summary>
        public void ClearHistory()
        {
            _queryHistory.Clear();
            SaveHistory();
        }

        public async Task AddQueryAsync(QueryHistoryItem item)
        {
            if (_queryHistory.Count >= 100) // Maximale Anzahl von Einträgen
            {
                _queryHistory.RemoveAt(_queryHistory.Count - 1);
            }
            _queryHistory.Insert(0, item);
            await SaveHistoryAsync();
        }

        private async Task SaveHistoryAsync()
        {
            try
            {
                var json = JsonSerializer.Serialize(_queryHistory);
                await File.WriteAllTextAsync(_historyFilePath, json);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Fehler beim Speichern des Query-Verlaufs");
            }
        }
    }
}
