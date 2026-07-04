using System.Windows.Controls;
using TradingSystem.Presentation.ViewModels;

namespace TradingSystem.Presentation.Views
{
    public partial class ChartPanel : UserControl
    {
        public ChartPanel(ChartViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
        }
    }
}