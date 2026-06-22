using System.Windows;
using SpacePilot.ViewModels;

namespace SpacePilot;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}
