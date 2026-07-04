using System.Windows.Controls;
using TradingSystem.Presentation.ViewModels;

namespace TradingSystem.Presentation.Views
{
    public partial class DataPanel : UserControl
    {
        public DataPanel(DataViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
       }
    }
}