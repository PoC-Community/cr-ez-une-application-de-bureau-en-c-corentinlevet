using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using TodoListApp.Models;

namespace TodoListApp;

public partial class MainWindow : Window
{
    private ObservableCollection<TaskItem> _tasks = new();
    private List<TaskItem> _allTasks = new();
    private const string DataFolder = "data";
    private const string JsonFilePath = "data/tasks.json";
    private const string BackupFilePath = "data/tasks.backup.json";
    private System.Threading.Timer? _autoSaveTimer;
    private bool _hasUnsavedChanges = false;

    public MainWindow()
    {
        InitializeComponent();
        TaskList.ItemsSource = _tasks;

        AddButton.Click += OnAddClick;
        DeleteButton.Click += OnDeleteClick;
        SaveButton.Click += OnSaveClick;
        FilterButton.Click += OnFilterClick;
        ClearFilterButton.Click += OnClearFilterClick;
        TodayFilterButton.Click += OnTodayFilterClick;
        WeekFilterButton.Click += OnWeekFilterClick;
        OverdueFilterButton.Click += OnOverdueFilterClick;
        CompleteAllButton.Click += OnCompleteAllClick;
        ClearCompletedButton.Click += OnClearCompletedClick;

        // Initialiser le timer d'auto-save (toutes les 30 secondes)
        _autoSaveTimer = new System.Threading.Timer(
            AutoSaveCallback, 
            null, 
            TimeSpan.FromSeconds(30), 
            TimeSpan.FromSeconds(30)
        );

        // Charger les tâches au démarrage avec récupération
        LoadTasksWithRecovery();
    }

    private void OnAddClick(object? sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(TaskInput.Text))
        {
            var task = new TaskItem 
            { 
                Title = TaskInput.Text,
                Tags = CleanupTags(TagsInput.Text ?? string.Empty),
                DueDate = DueDatePicker.SelectedDate?.DateTime
            };
            
            _tasks.Add(task);
            _allTasks.Add(task);
            
            TaskInput.Text = string.Empty;
            TagsInput.Text = string.Empty;
            DueDatePicker.SelectedDate = null;

            // Marquer les changements et auto-save
            MarkUnsavedChanges();
            AutoSaveImmediate();
        }
    }

    private string CleanupTags(string tags)
    {
        if (string.IsNullOrWhiteSpace(tags))
            return string.Empty;

        // Séparer par virgules, nettoyer les espaces, supprimer les doublons
        var tagList = tags.Split(',')
            .Select(t => t.Trim())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return string.Join(", ", tagList);
    }

    private void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (TaskList.SelectedItem is TaskItem selected)
        {
            _tasks.Remove(selected);
            _allTasks.Remove(selected);
            
            MarkUnsavedChanges();
            AutoSaveImmediate();
        }
    }

    private void OnFilterClick(object? sender, RoutedEventArgs e)
    {
        var filterTag = TagFilterInput.Text?.Trim().ToLower();
        
        if (string.IsNullOrWhiteSpace(filterTag))
        {
            StatusText.Text = "Please enter a tag to filter";
            StatusText.Foreground = Avalonia.Media.Brushes.Orange;
            return;
        }

        _tasks.Clear();
        var filteredTasks = _allTasks.Where(t => 
            !string.IsNullOrWhiteSpace(t.Tags) && 
            t.Tags.ToLower().Contains(filterTag)
        ).ToList();

        foreach (var task in filteredTasks)
        {
            _tasks.Add(task);
        }

        StatusText.Text = $"✓ Filtered by '{filterTag}' - {filteredTasks.Count} task(s) found";
        StatusText.Foreground = Avalonia.Media.Brushes.Blue;
    }

    private void OnClearFilterClick(object? sender, RoutedEventArgs e)
    {
        _tasks.Clear();
        foreach (var task in _allTasks)
        {
            _tasks.Add(task);
        }

        TagFilterInput.Text = string.Empty;
        StatusText.Text = $"✓ Filter cleared - showing all {_allTasks.Count} task(s)";
        StatusText.Foreground = Avalonia.Media.Brushes.Blue;
    }

    private void OnTodayFilterClick(object? sender, RoutedEventArgs e)
    {
        var today = DateTime.Now.Date;
        
        _tasks.Clear();
        var filteredTasks = _allTasks.Where(t => 
            t.DueDate.HasValue && 
            t.DueDate.Value.Date == today
        ).ToList();

        foreach (var task in filteredTasks)
        {
            _tasks.Add(task);
        }

        StatusText.Text = $"✓ Tasks due today - {filteredTasks.Count} task(s) found";
        StatusText.Foreground = Avalonia.Media.Brushes.Blue;
    }

    private void OnWeekFilterClick(object? sender, RoutedEventArgs e)
    {
        var today = DateTime.Now.Date;
        var weekFromNow = today.AddDays(7);
        
        _tasks.Clear();
        var filteredTasks = _allTasks.Where(t => 
            t.DueDate.HasValue && 
            t.DueDate.Value.Date >= today &&
            t.DueDate.Value.Date <= weekFromNow
        ).ToList();

        foreach (var task in filteredTasks)
        {
            _tasks.Add(task);
        }

        StatusText.Text = $"✓ Tasks due this week - {filteredTasks.Count} task(s) found";
        StatusText.Foreground = Avalonia.Media.Brushes.Blue;
    }

    private void OnOverdueFilterClick(object? sender, RoutedEventArgs e)
    {
        _tasks.Clear();
        var filteredTasks = _allTasks.Where(t => t.IsOverdue).ToList();

        foreach (var task in filteredTasks)
        {
            _tasks.Add(task);
        }

        StatusText.Text = $"⚠️ Overdue tasks - {filteredTasks.Count} task(s) found";
        StatusText.Foreground = Avalonia.Media.Brushes.Red;
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        AutoSaveImmediate();
        StatusText.Text = $"💾 Manual save complete! ({_allTasks.Count} tasks)";
        StatusText.Foreground = Avalonia.Media.Brushes.Green;
    }

    private void LoadTasks()
    {
        try
        {
            // Vérifier si le fichier existe
            if (File.Exists(JsonFilePath))
            {
                // Lire le contenu du fichier JSON
                string jsonString = File.ReadAllText(JsonFilePath);

                // Désérialiser le JSON en liste de TaskItem
                var tasks = JsonSerializer.Deserialize<List<TaskItem>>(jsonString);

                // Vider les collections et ajouter les tâches chargées
                _tasks.Clear();
                _allTasks.Clear();
                
                if (tasks != null)
                {
                    foreach (var task in tasks)
                    {
                        _tasks.Add(task);
                        _allTasks.Add(task);
                    }

                    StatusText.Text = $"✓ Loaded {tasks.Count} tasks from file";
                    StatusText.Foreground = Avalonia.Media.Brushes.Blue;
                }
            }
            else
            {
                // Fichier n'existe pas : démarrer avec une liste vide
                StatusText.Text = "No saved tasks found. Starting fresh!";
                StatusText.Foreground = Avalonia.Media.Brushes.Gray;
            }
        }
        catch (JsonException ex)
        {
            // JSON invalide ou corrompu
            StatusText.Text = $"✗ Invalid JSON file: {ex.Message}";
            StatusText.Foreground = Avalonia.Media.Brushes.Red;
        }
        catch (Exception ex)
        {
            // Autres erreurs (permissions, etc.)
            StatusText.Text = $"✗ Error loading tasks: {ex.Message}";
            StatusText.Foreground = Avalonia.Media.Brushes.Red;
        }
    }

    private void OnCompleteAllClick(object? sender, RoutedEventArgs e)
    {
        foreach (var task in _allTasks)
        {
            task.IsCompleted = true;
        }

        StatusText.Text = $"✓ All tasks marked as completed ({_allTasks.Count} tasks)";
        StatusText.Foreground = Avalonia.Media.Brushes.Green;
        
        MarkUnsavedChanges();
        AutoSaveImmediate();
    }

    private void OnClearCompletedClick(object? sender, RoutedEventArgs e)
    {
        var completedTasks = _allTasks.Where(t => t.IsCompleted).ToList();
        var count = completedTasks.Count;

        foreach (var task in completedTasks)
        {
            _tasks.Remove(task);
            _allTasks.Remove(task);
        }

        StatusText.Text = $"✓ Cleared {count} completed task(s)";
        StatusText.Foreground = Avalonia.Media.Brushes.Green;
        
        MarkUnsavedChanges();
        AutoSaveImmediate();
    }

    private async void OnTaskListDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        // Récupérer l'élément cliqué
        if (e.Source is Control control)
        {
            // Chercher le TaskItem dans le DataContext
            var item = control.DataContext as TaskItem;
            if (item == null)
            {
                // Essayer de remonter l'arbre visuel
                var parent = control.Parent;
                while (parent != null && item == null)
                {
                    item = parent.DataContext as TaskItem;
                    parent = parent.Parent;
                }
            }

            if (item != null)
            {
                // Créer une boîte de dialogue pour éditer le titre
                var dialog = new Window
                {
                    Title = "Edit Task",
                    Width = 400,
                    Height = 150,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };

                var stackPanel = new StackPanel { Margin = new Avalonia.Thickness(20) };
                
                var label = new TextBlock 
                { 
                    Text = "Edit task title:", 
                    Margin = new Avalonia.Thickness(0, 0, 0, 10) 
                };
                
                var textBox = new TextBox 
                { 
                    Text = item.Title,
                    Watermark = "Enter new title...",
                    Margin = new Avalonia.Thickness(0, 0, 0, 10)
                };

                var buttonPanel = new StackPanel 
                { 
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
                };

                var okButton = new Button 
                { 
                    Content = "OK", 
                    Width = 80,
                    Margin = new Avalonia.Thickness(0, 0, 10, 0)
                };
                
                var cancelButton = new Button 
                { 
                    Content = "Cancel", 
                    Width = 80 
                };

                okButton.Click += (s, args) =>
                {
                    if (!string.IsNullOrWhiteSpace(textBox.Text))
                    {
                        item.Title = textBox.Text;
                        
                        // Forcer le rafraîchissement de l'affichage
                        var index = _tasks.IndexOf(item);
                        if (index >= 0)
                        {
                            _tasks.RemoveAt(index);
                            _tasks.Insert(index, item);
                        }
                        
                        StatusText.Text = "✓ Task updated";
                        StatusText.Foreground = Avalonia.Media.Brushes.Green;
                        
                        MarkUnsavedChanges();
                        AutoSaveImmediate();
                    }
                    dialog.Close();
                };

                cancelButton.Click += (s, args) => dialog.Close();

                buttonPanel.Children.Add(okButton);
                buttonPanel.Children.Add(cancelButton);

                stackPanel.Children.Add(label);
                stackPanel.Children.Add(textBox);
                stackPanel.Children.Add(buttonPanel);

                dialog.Content = stackPanel;

                await dialog.ShowDialog(this);
            }
        }
    }

    private void MarkUnsavedChanges()
    {
        _hasUnsavedChanges = true;
    }

    private void AutoSaveCallback(object? state)
    {
        if (_hasUnsavedChanges)
        {
            Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => 
            {
                AutoSaveImmediate();
            });
        }
    }

    private void AutoSaveImmediate()
    {
        try
        {
            // Créer le dossier data s'il n'existe pas
            if (!Directory.Exists(DataFolder))
            {
                Directory.CreateDirectory(DataFolder);
            }

            // Créer une backup avant de sauvegarder
            if (File.Exists(JsonFilePath))
            {
                try
                {
                    File.Copy(JsonFilePath, BackupFilePath, overwrite: true);
                }
                catch
                {
                    // Ignorer les erreurs de backup
                }
            }

            // Sérialiser la collection complète de tâches
            var options = new JsonSerializerOptions 
            { 
                WriteIndented = true 
            };
            string jsonString = JsonSerializer.Serialize(_allTasks, options);

            // Écrire le JSON dans le fichier
            File.WriteAllText(JsonFilePath, jsonString);

            _hasUnsavedChanges = false;

            // Mise à jour subtile du statut
            var now = DateTime.Now.ToString("HH:mm:ss");
            StatusText.Text = $"💾 Auto-saved at {now} ({_allTasks.Count} tasks)";
            StatusText.Foreground = Avalonia.Media.Brushes.Gray;
        }
        catch (UnauthorizedAccessException)
        {
            StatusText.Text = "✗ Error: No permission to save file";
            StatusText.Foreground = Avalonia.Media.Brushes.Red;
        }
        catch (IOException ex)
        {
            StatusText.Text = $"✗ Error: Unable to save ({ex.Message})";
            StatusText.Foreground = Avalonia.Media.Brushes.Red;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"✗ Error saving: {ex.Message}";
            StatusText.Foreground = Avalonia.Media.Brushes.Red;
        }
    }

    private void LoadTasksWithRecovery()
    {
        try
        {
            // Essayer de charger le fichier principal
            if (File.Exists(JsonFilePath))
            {
                LoadFromFile(JsonFilePath);
            }
            else if (File.Exists(BackupFilePath))
            {
                // Si le fichier principal n'existe pas, essayer le backup
                StatusText.Text = "⚠️ Loading from backup file...";
                StatusText.Foreground = Avalonia.Media.Brushes.Orange;
                LoadFromFile(BackupFilePath);
            }
            else
            {
                // Aucun fichier trouvé
                StatusText.Text = "No saved tasks found. Starting fresh!";
                StatusText.Foreground = Avalonia.Media.Brushes.Gray;
            }
        }
        catch (JsonException)
        {
            // JSON corrompu, essayer le backup
            if (File.Exists(BackupFilePath))
            {
                try
                {
                    StatusText.Text = "⚠️ Main file corrupted. Restoring from backup...";
                    StatusText.Foreground = Avalonia.Media.Brushes.Orange;
                    LoadFromFile(BackupFilePath);
                }
                catch
                {
                    StatusText.Text = "✗ Both files corrupted. Starting fresh!";
                    StatusText.Foreground = Avalonia.Media.Brushes.Red;
                }
            }
            else
            {
                StatusText.Text = "✗ File corrupted and no backup. Starting fresh!";
                StatusText.Foreground = Avalonia.Media.Brushes.Red;
            }
        }
    }

    private void LoadFromFile(string filePath)
    {
        string jsonString = File.ReadAllText(filePath);
        var tasks = JsonSerializer.Deserialize<List<TaskItem>>(jsonString);

        _tasks.Clear();
        _allTasks.Clear();

        if (tasks != null)
        {
            foreach (var task in tasks)
            {
                _tasks.Add(task);
                _allTasks.Add(task);
            }

            StatusText.Text = $"✓ Loaded {tasks.Count} tasks";
            StatusText.Foreground = Avalonia.Media.Brushes.Blue;
        }
    }
}
