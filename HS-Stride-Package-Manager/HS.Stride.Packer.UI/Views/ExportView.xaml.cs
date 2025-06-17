using System.Windows.Controls;
using HS.Stride.Packer.UI.ViewModels;
using UserControl = System.Windows.Controls.UserControl;

namespace HS.Stride.Packer.UI.Views
{
    /// <summary>
    /// Interaction logic for ExportView.xaml
    /// </summary>
    public partial class ExportView : UserControl
    {
        public ExportView()
        {
            InitializeComponent();
            DataContext = new ExportViewModel();
        }
    }
}