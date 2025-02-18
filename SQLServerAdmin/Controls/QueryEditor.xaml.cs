using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using Microsoft.Win32;
using Serilog;
using SQLServerAdmin.Models;
using SQLServerAdmin.Services;
using SQLServerAdmin.Dialogs;
using System.Text.RegularExpressions;
using System.Xml;

namespace SQLServerAdmin.Controls
{
    public partial class QueryEditor : UserControl
    {
        private CompletionWindow? _completionWindow;
        private ToolTip? _toolTip;
        private FoldingManager? _foldingManager;
        private object? _foldingStrategy;
        private string? _selectedDatabase;
        private bool _isInitialized;
        private QueryExecutionService? _queryExecutionService;
        private QueryTemplateService? _templateService;
        private QueryHistoryService? _queryHistoryService;
        private QueryExportService? _exportService;
        private IntelliSenseService? _intelliSenseService;
        private QueryFormatterService? _formatterService;

        public static readonly DependencyProperty QueryExecutionServiceProperty =
            DependencyProperty.Register(nameof(QueryExecutionService), typeof(QueryExecutionService), typeof(QueryEditor),
                new PropertyMetadata(null, OnServiceChanged));

        public static readonly DependencyProperty HistoryServiceProperty =
            DependencyProperty.Register(nameof(HistoryService), typeof(QueryHistoryService), typeof(QueryEditor),
                new PropertyMetadata(null, OnServiceChanged));

        public static readonly DependencyProperty TemplateServiceProperty =
            DependencyProperty.Register(nameof(TemplateService), typeof(QueryTemplateService), typeof(QueryEditor),
                new PropertyMetadata(null, OnServiceChanged));

        public static readonly DependencyProperty ExportServiceProperty =
            DependencyProperty.Register(nameof(ExportService), typeof(QueryExportService), typeof(QueryEditor),
                new PropertyMetadata(null, OnServiceChanged));

        public static readonly DependencyProperty FormatterServiceProperty =
            DependencyProperty.Register(nameof(FormatterService), typeof(QueryFormatterService), typeof(QueryEditor),
                new PropertyMetadata(null, OnServiceChanged));

        public static readonly DependencyProperty IntelliSenseServiceProperty =
            DependencyProperty.Register(nameof(IntelliSenseService), typeof(IntelliSenseService), typeof(QueryEditor),
                new PropertyMetadata(null, OnServiceChanged));

        public QueryExecutionService? QueryExecutionService
        {
            get => _queryExecutionService;
            set => SetValue(QueryExecutionServiceProperty, value);
        }

        public QueryHistoryService? HistoryService
        {
            get => _queryHistoryService;
            set => SetValue(HistoryServiceProperty, value);
        }

        public QueryTemplateService? TemplateService
        {
            get => _templateService;
            set => SetValue(TemplateServiceProperty, value);
        }

        public QueryExportService? ExportService
        {
            get => _exportService;
            set => SetValue(ExportServiceProperty, value);
        }

        public QueryFormatterService? FormatterService
        {
            get => _formatterService;
            set => SetValue(FormatterServiceProperty, value);
        }

        public IntelliSenseService? IntelliSenseService
        {
            get => _intelliSenseService;
            set => SetValue(IntelliSenseServiceProperty, value);
        }

        public QueryEditor()
        {
            InitializeComponent();
            
            // Event-Handler registrieren
            ExecuteButton.Click += OnExecuteButtonClicked;
            ShowPlanButton.Click += OnShowPlanButtonClicked;
            FormatButton.Click += OnFormatButtonClicked;
            ExportCsvButton.Click += OnExportCsvButtonClicked;
            ExportExcelButton.Click += OnExportExcelButtonClicked;
            TemplatesComboBox.SelectionChanged += OnTemplatesComboBoxSelectionChanged;
            SaveTemplateButton.Click += OnSaveTemplateButtonClicked;

            Loaded += (s, e) =>
            {
                if (!_isInitialized)
                {
                    _isInitialized = true;
                    LoadTemplatesAsync();
                }
            };

            QueryTextEditor.TextChanged += OnQueryTextEditorTextChanged;
            QueryTextEditor.PreviewKeyDown += OnQueryTextEditorPreviewKeyDown;

            // Editor-Einstellungen
            QueryTextEditor.ShowLineNumbers = true;
            QueryTextEditor.Options.EnableHyperlinks = false;
            QueryTextEditor.Options.EnableEmailHyperlinks = false;
            
            // Syntax-Highlighting laden
            InitializeSyntaxHighlighting();
            
            // Events für Auto-Vervollständigung
            QueryTextEditor.TextArea.TextEntered += TextArea_TextEntered;
            QueryTextEditor.TextArea.TextEntering += TextArea_TextEntering;
            
            // ToolTip für Fehler
            _toolTip = new ToolTip { MaxWidth = 400 };
            QueryTextEditor.TextArea.TextView.MouseHover += TextView_MouseHover;
            QueryTextEditor.TextArea.TextView.MouseHoverStopped += TextView_MouseHoverStopped;

            // Tastaturkürzel registrieren
            RegisterKeyboardShortcuts();
        }

        private static void OnServiceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is QueryEditor editor)
            {
                switch (e.Property.Name)
                {
                    case nameof(QueryExecutionService):
                        editor._queryExecutionService = e.NewValue as QueryExecutionService;
                        break;
                    case nameof(HistoryService):
                        editor._queryHistoryService = e.NewValue as QueryHistoryService;
                        break;
                    case nameof(TemplateService):
                        editor._templateService = e.NewValue as QueryTemplateService;
                        Task.Run(() => editor.LoadTemplatesAsync()); // Asynchronen Aufruf starten ohne zu warten
                        break;
                    case nameof(ExportService):
                        editor._exportService = e.NewValue as QueryExportService;
                        break;
                    case nameof(FormatterService):
                        editor._formatterService = e.NewValue as QueryFormatterService;
                        break;
                    case nameof(IntelliSenseService):
                        editor._intelliSenseService = e.NewValue as IntelliSenseService;
                        break;
                }
            }
        }

        private async Task LoadTemplatesAsync()
        {
            try
            {
                if (_templateService != null)
                {
                    var templates = await _templateService.GetTemplatesAsync();
                    await Dispatcher.InvokeAsync(() =>
                    {
                        TemplatesComboBox.ItemsSource = templates;
                        TemplatesComboBox.DisplayMemberPath = "Name";
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Fehler beim Laden der Templates");
                await Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show("Fehler beim Laden der Templates: " + ex.Message);
                });
            }
        }

        private void RegisterKeyboardShortcuts()
        {
            // Tastaturkürzel definieren
            var keyBindings = new[]
            {
                new { Key = Key.F5, Modifiers = ModifierKeys.None, Command = new RelayCommand(_ => OnExecuteButtonClicked(this, new RoutedEventArgs())) },
                new { Key = Key.E, Modifiers = ModifierKeys.Control, Command = new RelayCommand(_ => OnExecuteButtonClicked(this, new RoutedEventArgs())) },
                new { Key = Key.F, Modifiers = ModifierKeys.Control, Command = new RelayCommand(_ => OnFormatButtonClicked(this, new RoutedEventArgs())) },
                new { Key = Key.S, Modifiers = ModifierKeys.Control, Command = new RelayCommand(_ => OnSaveTemplateButtonClicked(this, new RoutedEventArgs())) },
                new { Key = Key.Space, Modifiers = ModifierKeys.Control, Command = new RelayCommand(_ => ShowCompletion()) }
            };

            // Tastaturkürzel registrieren
            foreach (var binding in keyBindings)
            {
                var keyBinding = new KeyBinding(
                    binding.Command,
                    binding.Key,
                    binding.Modifiers
                );
                QueryTextEditor.InputBindings.Add(keyBinding);
            }
        }

        private void TextArea_TextEntered(object sender, TextCompositionEventArgs e)
        {
            if (_intelliSenseService == null) return;

            if (e.Text == "." || e.Text == " ")
            {
                ShowCompletion();
            }
        }

        private void TextArea_TextEntering(object sender, TextCompositionEventArgs e)
        {
            if (e.Text.Length > 0 && _completionWindow != null)
            {
                if (!char.IsLetterOrDigit(e.Text[0]))
                {
                    _completionWindow.CompletionList.RequestInsertion(e);
                }
            }
        }

        private async void ShowCompletion()
        {
            if (_intelliSenseService == null) return;

            try
            {
                var caretOffset = QueryTextEditor.CaretOffset;
                var document = QueryTextEditor.Document;
                var line = document.GetLineByOffset(caretOffset);
                var lineText = document.GetText(line.Offset, caretOffset - line.Offset);

                // Letztes Wort vor dem Cursor finden
                var match = Regex.Match(lineText, @"[\w\d_]+$");
                var partialWord = match.Success ? match.Value : string.Empty;

                var suggestions = await _intelliSenseService.GetSuggestionsAsync(partialWord, _selectedDatabase);
                
                if (suggestions.Count == 0) return;

                var completionWindow = new CompletionWindow(QueryTextEditor.TextArea);
                var data = completionWindow.CompletionList.CompletionData;

                foreach (var item in suggestions)
                {
                    data.Add(new CompletionData(item, async () =>
                    {
                        var info = await _intelliSenseService.GetObjectInfoAsync(item, _selectedDatabase);
                        return info;
                    }));
                }

                completionWindow.Show();
                completionWindow.Closed += (sender, args) =>
                {
                    completionWindow = null;
                };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Fehler beim Anzeigen der Vervollständigung");
                MessageBox.Show("Fehler beim Anzeigen der Vervollständigung: " + ex.Message);
            }
        }

        private void TextView_MouseHover(object sender, MouseEventArgs e)
        {
            var position = QueryTextEditor.TextArea.TextView.GetPositionFloor(e.GetPosition(QueryTextEditor.TextArea.TextView) + QueryTextEditor.TextArea.TextView.ScrollOffset);
            if (position.HasValue)
            {
                var offset = QueryTextEditor.Document.GetOffset(position.Value.Line, position.Value.Column);
                var word = GetWordAtOffset(offset);
                
                if (!string.IsNullOrEmpty(word))
                {
                    ShowTooltip(word, e);
                }
            }
        }

        private void TextView_MouseHoverStopped(object sender, MouseEventArgs e)
        {
            _toolTip.IsOpen = false;
        }

        private string GetWordAtOffset(int offset)
        {
            var text = QueryTextEditor.Document.Text;
            var start = offset;
            var end = offset;

            while (start > 0 && (char.IsLetterOrDigit(text[start - 1]) || text[start - 1] == '_'))
                start--;

            while (end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] == '_'))
                end++;

            return text.Substring(start, end - start);
        }

        private async void ShowTooltip(string word, MouseEventArgs e)
        {
            if (_intelliSenseService == null) return;

            try
            {
                var info = await _intelliSenseService.GetObjectInfoAsync(word, _selectedDatabase);

                if (!string.IsNullOrEmpty(info))
                {
                    _toolTip.Content = info;
                    _toolTip.IsOpen = true;
                    _toolTip.PlacementTarget = QueryTextEditor;
                    _toolTip.Placement = System.Windows.Controls.Primitives.PlacementMode.Mouse;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Fehler beim Laden der Tooltip-Information");
            }
        }

        private async void OnExecuteButtonClicked(object sender, RoutedEventArgs e)
        {
            await ExecuteQuery();
        }

        private async void OnShowPlanButtonClicked(object sender, RoutedEventArgs e)
        {
            if (_queryExecutionService == null) return;

            try
            {
                StatusLabel.Text = "Generiere Ausführungsplan...";
                var query = QueryTextEditor.Text;

                // Ausführungsplan abrufen
                var plan = await _queryExecutionService.GetQueryPlanAsync(query);
                
                if (!string.IsNullOrEmpty(plan))
                {
                    // Ausführungsplan im Browser anzeigen
                    ExecutionPlanBrowser.NavigateToString(plan);
                    
                    // Zum Plan-Tab wechseln
                    var planTab = ResultTabs.Items.Cast<TabItem>()
                        .FirstOrDefault(t => t.Header.ToString() == "Ausführungsplan");
                    
                    if (planTab != null)
                    {
                        ResultTabs.SelectedItem = planTab;
                    }
                    
                    StatusLabel.Text = "Ausführungsplan generiert.";
                }
                else
                {
                    StatusLabel.Text = "Konnte keinen Ausführungsplan generieren.";
                    MessagesTextBox.Text = "Fehler beim Generieren des Ausführungsplans.";
                }
            }
            catch (Exception ex)
            {
                StatusLabel.Text = "Fehler beim Generieren des Ausführungsplans";
                MessagesTextBox.Text = ex.Message;
                Log.Error(ex, "Fehler beim Generieren des Ausführungsplans");
            }
        }

        private async void OnFormatButtonClicked(object sender, RoutedEventArgs e)
        {
            await FormatQuery();
        }

        private async Task FormatQuery()
        {
            if (_queryExecutionService == null) return;

            try
            {
                var query = QueryTextEditor.Text;
                if (string.IsNullOrWhiteSpace(query)) return;

                var formattedQuery = await _queryExecutionService.FormatQueryAsync(query);
                if (!string.IsNullOrWhiteSpace(formattedQuery))
                {
                    QueryTextEditor.Text = formattedQuery;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Fehler beim Formatieren der Query");
                MessageBox.Show($"Fehler beim Formatieren: {ex.Message}");
            }
        }

        private async void OnExportCsvButtonClicked(object sender, RoutedEventArgs e)
        {
            await ExportToCsv();
        }

        private async void OnExportExcelButtonClicked(object sender, RoutedEventArgs e)
        {
            await ExportToExcel();
        }

        private void OnSaveTemplateButtonClicked(object sender, RoutedEventArgs e)
        {
            _ = SaveAsTemplateAsync();
        }

        private async Task SaveAsTemplate()
        {
            if (_templateService == null) return;

            try
            {
                var dialog = new SaveTemplateDialog();
                if (dialog.ShowDialog() == true)
                {
                    var template = new Models.QueryTemplate
                    {
                        Name = dialog.TemplateName,
                        Query = QueryTextEditor.Text,
                        Description = dialog.Description
                    };

                    await _templateService.SaveTemplateAsync(template);
                    await LoadTemplatesAsync();
                    MessageBox.Show("Template erfolgreich gespeichert!");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Fehler beim Speichern des Templates");
                MessageBox.Show("Fehler beim Speichern: " + ex.Message);
            }
        }

        private async Task SaveAsTemplateAsync()
        {
            var dialog = new SaveTemplateDialog();
            if (dialog.ShowDialog() == true)
            {
                if (_templateService != null && QueryTextEditor != null)
                {
                    await _templateService.SaveTemplateAsync(new Models.QueryTemplate
                    {
                        Name = dialog.TemplateName,
                        Description = dialog.Description,
                        Query = QueryTextEditor.Text
                    });
                }
            }
        }

        private void OnTemplatesComboBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TemplatesComboBox.SelectedItem is Models.QueryTemplate template)
            {
                QueryTextEditor.Text = template.Query;
            }
        }

        private void ClearResultsButton_Click(object sender, RoutedEventArgs e)
        {
            ResultsGrid.ItemsSource = null;
            UpdateStatus("Bereit");
        }

        private void ShowLineNumbersCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            // Diese Funktion wird nicht mehr benötigt, da ShowLineNumbers in XAML gesetzt ist
        }

        private void FontSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Diese Funktion wird nicht mehr benötigt, da FontSize in XAML fest auf 12 gesetzt ist
        }

        private void FormatQueryButton_Click(object sender, RoutedEventArgs e)
        {
            if (_formatterService == null) return;
            
            var query = QueryTextEditor.Text;
            if (string.IsNullOrWhiteSpace(query)) return;

            var formattedQuery = _formatterService.FormatQuery(query);
            QueryTextEditor.Text = formattedQuery;
        }

        private void CommentButton_Click(object sender, RoutedEventArgs e)
        {
            var selectionStart = QueryTextEditor.SelectionStart;
            var selectionLength = QueryTextEditor.SelectionLength;
            
            if (selectionLength == 0) return;

            var text = QueryTextEditor.Text;
            var commentedText = _formatterService.CommentLines(
                text,
                selectionStart,
                selectionLength
            );

            QueryTextEditor.Text = commentedText;
        }

        private void ResultViewModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateResultsView();
        }

        private void UpdateResultsView(object result = null)
        {
            ClearResults();
            if (result != null)
            {
                if (ResultsGrid != null && result is System.Collections.IEnumerable enumerable)
                {
                    ResultsGrid.ItemsSource = enumerable;
                }
            }
        }

        private void UpdateStatus(string? message = null)
        {
            if (StatusLabel != null)
            {
                StatusLabel.Text = message ?? "Bereit";
            }
        }

        private async Task ExportToCsvAsync(string filePath)
        {
            if (ResultsGrid?.ItemsSource is DataTable data && data != null)
            {
                await _exportService?.ExportToCsvAsync(data, filePath);
            }
        }

        private async Task ExportToExcelAsync(string filePath)
        {
            if (ResultsGrid?.ItemsSource is DataTable data && data != null)
            {
                await _exportService?.ExportToExcelAsync(data, filePath);
            }
        }

        private void ClearResults()
        {
            ResultTabs.Items.Clear();
            MessagesTextBox.Clear();
            ExecutionPlanBrowser.NavigateToString("");
            StatusLabel.Text = "Bereit";
        }

        private void OnQueryTextEditorTextChanged(object sender, EventArgs e)
        {
            // Speichere den aktuellen Text und aktualisiere die UI
            if (QueryTextEditor != null)
            {
                UpdateResultsView();
                if (_queryHistoryService != null)
                {
                    var historyItem = new Models.QueryHistoryItem 
                    { 
                        Query = QueryTextEditor.Text,
                        ExecutionTime = DateTime.Now,
                        Database = "", // Aktuelle Datenbank könnte hier gesetzt werden
                        Success = true
                    };
                    _queryHistoryService.QueryHistory.Add(historyItem);
                }
            }
        }

        private async void OnQueryTextEditorPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5)
            {
                await ExecuteQuery();
                e.Handled = true;
            }
        }

        private async Task ExecuteQuery()
        {
            if (_queryExecutionService == null) return;

            try
            {
                StatusLabel.Text = "Führe Query aus...";
                var query = QueryTextEditor.Text;
                
                if (string.IsNullOrWhiteSpace(query))
                {
                    MessageBox.Show("Bitte geben Sie eine Query ein.");
                    return;
                }

                var result = await _queryExecutionService.ExecuteQueryAsync(query, _selectedDatabase);
                
                if (result != null)
                {
                    UpdateResultsView(result);
                    await _queryHistoryService.AddQueryAsync(new Models.QueryHistoryItem 
                    { 
                        Query = query,
                        ExecutionTime = DateTime.Now,
                        Database = _selectedDatabase
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Fehler beim Ausführen der Query");
                MessageBox.Show("Fehler beim Ausführen der Query: " + ex.Message);
                StatusLabel.Text = "Fehler bei der Ausführung";
            }
        }

        private async Task ExportToCsv()
        {
            if (_exportService == null) return;

            try
            {
                var selectedTab = ResultTabs.SelectedItem as TabItem;
                if (selectedTab?.Content is DataGrid grid && grid.ItemsSource is DataView view)
                {
                    var dialog = new SaveFileDialog
                    {
                        Filter = "CSV-Dateien (*.csv)|*.csv",
                        DefaultExt = ".csv"
                    };

                    if (dialog.ShowDialog() == true)
                    {
                        await _exportService.ExportToCsvAsync(view.Table, dialog.FileName);
                        MessageBox.Show("Export erfolgreich abgeschlossen!");
                    }
                }
                else
                {
                    MessageBox.Show("Bitte wählen Sie zuerst ein Ergebnis aus.");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Fehler beim CSV-Export");
                MessageBox.Show("Fehler beim Export: " + ex.Message);
            }
        }

        private async Task ExportToExcel()
        {
            if (_exportService == null) return;

            try
            {
                var selectedTab = ResultTabs.SelectedItem as TabItem;
                if (selectedTab?.Content is DataGrid grid && grid.ItemsSource is DataView view)
                {
                    var dialog = new SaveFileDialog
                    {
                        Filter = "Excel-Dateien (*.xlsx)|*.xlsx",
                        DefaultExt = ".xlsx"
                    };

                    if (dialog.ShowDialog() == true)
                    {
                        await _exportService.ExportToExcelAsync(view.Table, dialog.FileName);
                        MessageBox.Show("Export erfolgreich abgeschlossen!");
                    }
                }
                else
                {
                    MessageBox.Show("Bitte wählen Sie zuerst ein Ergebnis aus.");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Fehler beim Excel-Export");
                MessageBox.Show("Fehler beim Export: " + ex.Message);
            }
        }

        private void UpdateTextBoxSettings()
        {
            if (QueryTextEditor != null)
            {
                // Die Zeilennummern sind jetzt direkt in XAML als "True" gesetzt
                // ShowLineNumbers wird über die XAML-Property gesteuert
                
                // Die Schriftgröße ist in XAML fest auf 12 gesetzt
                // Falls später eine dynamische Anpassung gewünscht ist, 
                // müsste ein entsprechendes UI-Element hinzugefügt werden
            }
        }

        private void InitializeSyntaxHighlighting()
        {
            try
            {
                // SQL-Syntax-Highlighting aktivieren
                var assembly = typeof(QueryEditor).Assembly;
                using var stream = assembly.GetManifestResourceStream("SQLServerAdmin.Resources.SQL.xshd");
                if (stream != null)
                {
                    using var reader = new System.Xml.XmlTextReader(stream);
                    QueryTextEditor.SyntaxHighlighting = ICSharpCode.AvalonEdit.Highlighting.Xshd.HighlightingLoader.Load(reader, ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Fehler beim Laden der SQL-Syntax-Hervorhebung");
            }
        }
    }

    public class QueryCompletionData : ICompletionData
    {
        public QueryCompletionData(string text)
        {
            Text = text;
        }

        public System.Windows.Media.ImageSource? Image => null;

        public string Text { get; }

        public object Content => Text;

        public object Description => "";

        public double Priority => 0;

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            textArea.Document.Replace(completionSegment, Text);
        }
    }

    public class CompletionData : ICompletionData
    {
        private readonly string _text;
        private readonly Func<Task<string>> _getInfo;
        private string? _description;

        public CompletionData(string text, Func<Task<string>> getInfo)
        {
            _text = text;
            _getInfo = getInfo;
        }

        public System.Windows.Media.ImageSource? Image => null;

        public string Text => _text;

        public object Content => Text;

        public object Description
        {
            get
            {
                if (_description == null)
                {
                    _description = _getInfo().GetAwaiter().GetResult();
                }
                return _description;
            }
        }

        public double Priority => 0;

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            textArea.Document.Replace(completionSegment, Text);
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Func<object, bool>? _canExecute;

        public RelayCommand(Action<object> execute, Func<object, bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object? parameter)
        {
            return _canExecute == null || _canExecute(parameter!);
        }

        public void Execute(object? parameter)
        {
            _execute(parameter!);
        }
    }
}
