using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace TradingSystem.Presentation.ViewModels
{
    public class NavigationViewModel : ObservableObject
    {
        private UserControl? _currentView;

        public UserControl? CurrentView
        {
            get => _currentView;
            set => SetProperty(ref _currentView, value);
        }
    }
}
