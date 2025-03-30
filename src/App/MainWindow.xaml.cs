using InteractiveBrokers;
using System.Windows;

namespace Trading_Assistant;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly IBClient _ibClient = new IBClient();

    public MainWindow()
    {
        InitializeComponent();

        // Invoke asynchronous method to tickle the IB client
        // This is a workaround for the async void issue
        // In a real application, consider using async Task and await
        // or handle the async method properly

        Dispatcher.BeginInvoke(new Action(async () => {
            await Task.Run(() => _ibClient.Tickle());
        }));
    }
}