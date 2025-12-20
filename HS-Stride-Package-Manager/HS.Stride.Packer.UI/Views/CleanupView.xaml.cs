// HS Stride Packer (c) 2025 Happenstance Games LLC - Apache License 2.0

using System.Windows.Controls;
using HS.Stride.Packer.UI.ViewModels;
using UserControl = System.Windows.Controls.UserControl;

namespace HS.Stride.Packer.UI.Views
{
    /// <summary>
    /// Interaction logic for CleanupView.xaml
    /// </summary>
    public partial class CleanupView : UserControl
    {
        public CleanupView()
        {
            InitializeComponent();
            DataContext = new CleanupViewModel();
        }
    }
}
