using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using TodoListApp.Models;

namespace TodoListApp;

public partial class MainWindow : Window
{
    private ObservableCollection<TaskItem> _tasks = new();
    private const string DataFolder = "data";
    private const string JsonFilePath = "data/tasks.json";

    public MainWindow()
    {
        InitializeComponent();
        TaskList.ItemsSource = _tasks;

        AddButton.Click += OnAddClick;
        DeleteButton.Click += OnDeleteClick;
        SaveButton.Click += OnSaveClick;

        // Charger les tâches au démarrage
        LoadTasks();
    }

    private void OnAddClick(object? sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(TaskInput.Text))
        {
            _tasks.Add(new TaskItem { Title = TaskInput.Text });
            TaskInput.Text = string.Empty;
        }
    }

    private void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (TaskList.SelectedItem is TaskItem selected)
        {
            _tasks.Remove(selected);
        }
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

            // Sérialiser la collection de tâches en JSON avec indentation
            var options = new JsonSerializerOptions 
            { 
                WriteIndented = true 
            };
            string jsonString = JsonSerializer.Serialize(_tasks, options);

            // Écrire le JSON dans le fichier
            File.WriteAllText(JsonFilePath, jsonString);

            // Afficher un message de confirmation
            StatusText.Text = $"✓ Tasks saved successfully! ({_tasks.Count} tasks)";
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

                // Vider la collection actuelle et ajouter les tâches chargées
                _tasks.Clear();
                if (tasks != null)
                {
                    foreach (var task in tasks)
                    {
                        _tasks.Add(task);
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
}
