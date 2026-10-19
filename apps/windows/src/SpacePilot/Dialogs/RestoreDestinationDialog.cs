using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using SpacePilot.Models;

namespace SpacePilot.Dialogs;

public sealed class RestoreDestinationDialog : Window
{
    public RestoreDestinationDialog(QuarantineEntry entry)
    {
        Title = "Restore to another location";
        Width = 620;
        SizeToContent = SizeToContent.Height;
        MinHeight = 220;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Owner = Application.Current?.MainWindow;

        var directory = Path.GetDirectoryName(entry.OriginalPath)
            ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var name = Path.GetFileNameWithoutExtension(entry.OriginalPath);
        var extension = entry.Kind == CleanupTargetKind.File ? Path.GetExtension(entry.OriginalPath) : string.Empty;
        var suggestedPath = Path.Combine(directory, $"{name}-restored{extension}");

        var content = new StackPanel { Margin = new Thickness(20) };
        content.Children.Add(new TextBlock
        {
            Text = $"Choose a new path for {entry.DisplayName}. Existing files and folders will never be overwritten.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 14)
        });
        var destination = new TextBox
        {
            Text = suggestedPath,
            MinHeight = 34,
            Padding = new Thickness(8, 6, 8, 6)
        };
        AutomationProperties.SetName(destination, "Alternate restore destination path");
        content.Children.Add(new Label { Content = "Destination path", Target = destination });
        content.Children.Add(destination);

        var error = new TextBlock
        {
            Foreground = System.Windows.Media.Brushes.Firebrick,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 8)
        };
        content.Children.Add(error);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "Cancel", MinWidth = 90, IsCancel = true, Margin = new Thickness(0, 0, 8, 0) };
        var restore = new Button { Content = "Restore", MinWidth = 90, IsDefault = true };
        restore.Click += (_, _) =>
        {
            try
            {
                var rawPath = destination.Text.Trim();
                if (!Path.IsPathFullyQualified(rawPath))
                {
                    error.Text = "Choose a full destination path.";
                    return;
                }

                var path = Path.GetFullPath(rawPath);
                if (File.Exists(path) || Directory.Exists(path))
                {
                    error.Text = "Choose a full path that does not already exist.";
                    return;
                }

                DestinationPath = path;
                DialogResult = true;
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                error.Text = $"Enter a valid destination path: {ex.Message}";
            }
        };
        buttons.Children.Add(cancel);
        buttons.Children.Add(restore);
        content.Children.Add(buttons);
        Content = content;
    }

    public string? DestinationPath { get; private set; }
}
