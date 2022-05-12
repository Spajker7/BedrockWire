using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace BedrockWire.Views
{
    public partial class SpinnerDialog : Window
    {
        public SpinnerDialog()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
