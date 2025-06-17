using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace HS.Stride.Packer.UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            
            // Set window title with version
            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.8.0";
            Title = $"HS Stride Packer v{version} - © 2025 Happenstance Games";
            
            // Wire up the navigation buttons
            ExportTabBtn.Click += (s, e) => SwitchTab(0);
            ImportTabBtn.Click += (s, e) => SwitchTab(1);
            StoreTabBtn.Click += (s, e) => SwitchTab(2);
        }
        
        private void SwitchTab(int tabIndex)
        {
            MainTabControl.SelectedIndex = tabIndex;
            
            // Update button styles
            ResetButtonStyles();
            
            switch (tabIndex)
            {
                case 0:
                    ExportTabBtn.Background = (System.Windows.Media.Brush)FindResource("StrideBlue");
                    ExportTabBtn.Foreground = System.Windows.Media.Brushes.White;
                    ExportTabBtn.FontWeight = FontWeights.SemiBold;
                    break;
                case 1:
                    ImportTabBtn.Background = (System.Windows.Media.Brush)FindResource("StrideBlue");
                    ImportTabBtn.Foreground = System.Windows.Media.Brushes.White;
                    ImportTabBtn.FontWeight = FontWeights.SemiBold;
                    break;
                case 2:
                    StoreTabBtn.Background = (System.Windows.Media.Brush)FindResource("StrideBlue");
                    StoreTabBtn.Foreground = System.Windows.Media.Brushes.White;
                    StoreTabBtn.FontWeight = FontWeights.SemiBold;
                    break;
            }
        }
        
        private void ResetButtonStyles()
        {
            var defaultBrush = System.Windows.Media.Brushes.Transparent;
            var defaultForeground = (System.Windows.Media.Brush)new BrushConverter().ConvertFrom("#6C757D");
            
            ExportTabBtn.Background = defaultBrush;
            ExportTabBtn.Foreground = defaultForeground;
            ExportTabBtn.FontWeight = FontWeights.Medium;
            
            ImportTabBtn.Background = defaultBrush;
            ImportTabBtn.Foreground = defaultForeground;
            ImportTabBtn.FontWeight = FontWeights.Medium;
            
            StoreTabBtn.Background = defaultBrush;
            StoreTabBtn.Foreground = defaultForeground;
            StoreTabBtn.FontWeight = FontWeights.Medium;
        }
    }
}