using AppCore.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace TradingAssistant;

public sealed partial class AccountDetailsControl : UserControl {
    public AccountDetailsControl() {
        InitializeComponent();
    }

    public static readonly DependencyProperty AccountProperty =
            DependencyProperty.Register(
                nameof(Account),
                typeof(Account),
                typeof(AccountDetailsControl),
                new PropertyMetadata(default(Account?)));

    public Account? Account {
        get => (Account?)GetValue(AccountProperty);
        set => SetValue(AccountProperty, value);
    }
}