using System.Windows.Controls;
using HS.Stride.Packer.UI.ViewModels;
using UserControl = System.Windows.Controls.UserControl;

namespace HS.Stride.Packer.UI.Views
{
    /// <summary>
    /// Interaction logic for ImportView.xaml
    /// </summary>
    public partial class ImportView : UserControl
    {
        public ImportView()
        {
            InitializeComponent();
            DataContext = new ImportViewModel();
        }
    }
}