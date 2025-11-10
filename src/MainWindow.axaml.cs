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

        // Charger les tâches au démarrage
        LoadTasks();
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
        try
        {
            // Créer le dossier data s'il n'existe pas
            if (!Directory.Exists(DataFolder))
            {
                Directory.CreateDirectory(DataFolder);
            }

            // Sérialiser la collection complète de tâches (pas juste la vue filtrée)
            var options = new JsonSerializerOptions 
            { 
                WriteIndented = true 
            };
            string jsonString = JsonSerializer.Serialize(_allTasks, options);

            // Écrire le JSON dans le fichier
            File.WriteAllText(JsonFilePath, jsonString);

            // Afficher un message de confirmation
            StatusText.Text = $"✓ Tasks saved successfully! ({_allTasks.Count} tasks)";
            StatusText.Foreground = Avalonia.Media.Brushes.Green;
        }
        catch (Exception ex)
        {
            // Gérer les erreurs (permissions, espace disque, etc.)
            StatusText.Text = $"✗ Error saving tasks: {ex.Message}";
            StatusText.Foreground = Avalonia.Media.Brushes.Red;
        }
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
}
