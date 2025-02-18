using System.Windows.Input;
using SQLServerAdmin.Commands;
using SQLServerAdmin.Services;

namespace SQLServerAdmin.ViewModels
{
    public class AIAssistantViewModel : ViewModelBase
    {
        private readonly AIAssistantService _aiService;
        private string _selectedQuery = string.Empty;
        private string _selectedTable = string.Empty;
        private string _aiResponse = string.Empty;
        private string _errorMessage = string.Empty;

        public AIAssistantViewModel(AIAssistantService aiService)
        {
            _aiService = aiService;
            
            OptimizeQueryCommand = new AsyncRelayCommand(OptimizeQueryAsync);
            GenerateDocumentationCommand = new AsyncRelayCommand(GenerateDocumentationAsync);
            GenerateQueryCommand = new AsyncRelayCommand(GenerateQueryFromNaturalLanguageAsync);
            ReviewQueryCommand = new AsyncRelayCommand(ReviewQueryAsync);
            AnalyzeIndexesCommand = new AsyncRelayCommand(AnalyzeIndexesAsync);
            AnalyzePerformanceCommand = new AsyncRelayCommand(AnalyzePerformanceAsync);
            SuggestErrorFixCommand = new AsyncRelayCommand(SuggestErrorFixAsync);
        }

        public string SelectedQuery
        {
            get => _selectedQuery;
            set
            {
                if (SetProperty(ref _selectedQuery, value))
                {
                    OnPropertyChanged(nameof(CanExecuteQueryCommands));
                }
            }
        }

        public string SelectedTable
        {
            get => _selectedTable;
            set
            {
                if (SetProperty(ref _selectedTable, value))
                {
                    OnPropertyChanged(nameof(CanExecuteTableCommands));
                }
            }
        }

        public string AIResponse
        {
            get => _aiResponse;
            set => SetProperty(ref _aiResponse, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public bool CanExecuteQueryCommands => !string.IsNullOrWhiteSpace(SelectedQuery);
        public bool CanExecuteTableCommands => !string.IsNullOrWhiteSpace(SelectedTable);

        public ICommand OptimizeQueryCommand { get; }
        public ICommand GenerateDocumentationCommand { get; }
        public ICommand GenerateQueryCommand { get; }
        public ICommand ReviewQueryCommand { get; }
        public ICommand AnalyzeIndexesCommand { get; }
        public ICommand AnalyzePerformanceCommand { get; }
        public ICommand SuggestErrorFixCommand { get; }

        private async Task OptimizeQueryAsync()
        {
            try
            {
                AIResponse = await _aiService.OptimizeQueryAsync(SelectedQuery);
                ErrorMessage = string.Empty;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Fehler bei der Query-Optimierung: {ex.Message}";
            }
        }

        private async Task GenerateDocumentationAsync()
        {
            try
            {
                AIResponse = await _aiService.GenerateDocumentationAsync(SelectedQuery);
                ErrorMessage = string.Empty;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Fehler bei der Dokumentationsgenerierung: {ex.Message}";
            }
        }

        private async Task GenerateQueryFromNaturalLanguageAsync()
        {
            try
            {
                AIResponse = await _aiService.GenerateQueryFromNaturalLanguageAsync(SelectedQuery, "CurrentDatabase");
                ErrorMessage = string.Empty;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Fehler bei der Query-Generierung: {ex.Message}";
            }
        }

        private async Task ReviewQueryAsync()
        {
            try
            {
                AIResponse = await _aiService.ReviewQueryAsync(SelectedQuery);
                ErrorMessage = string.Empty;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Fehler beim Query-Review: {ex.Message}";
            }
        }

        private async Task AnalyzeIndexesAsync()
        {
            try
            {
                AIResponse = await _aiService.AnalyzeIndexesAsync(SelectedTable);
                ErrorMessage = string.Empty;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Fehler bei der Index-Analyse: {ex.Message}";
            }
        }

        private async Task AnalyzePerformanceAsync()
        {
            try
            {
                AIResponse = await _aiService.AnalyzePerformanceAsync(SelectedQuery);
                ErrorMessage = string.Empty;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Fehler bei der Performance-Analyse: {ex.Message}";
            }
        }

        private async Task SuggestErrorFixAsync()
        {
            try
            {
                AIResponse = await _aiService.SuggestErrorFixAsync(ErrorMessage, SelectedQuery);
                ErrorMessage = string.Empty;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Fehler bei der Fehlerbehebung: {ex.Message}";
            }
        }
    }
}
