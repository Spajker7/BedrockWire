using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using BedrockWire.ViewModels;
using System.ComponentModel;

namespace BedrockWire.Views
{
    public partial class StartProxyDialog : Window
    {
        public StartProxyDialog()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            Closing += (object? sender, CancelEventArgs args) =>
            {
                var vm = (StartProxyDialogViewModel) DataContext;
                if (!vm.NormallyClosing)
                {
                    args.Cancel = true;
                }
            };
        }
    }
}
