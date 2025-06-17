using System.Windows.Controls;
using HS.Stride.Packer.UI.ViewModels;
using UserControl = System.Windows.Controls.UserControl;

namespace HS.Stride.Packer.UI.Views
{
    /// <summary>
    /// Interaction logic for StoreView.xaml
    /// </summary>
    public partial class StoreView : UserControl
    {
        public StoreView()
        {
            InitializeComponent();
            DataContext = new StoreViewModel();
        }
    }
}