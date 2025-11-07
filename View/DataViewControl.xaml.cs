using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Nivtropy.ViewModels;

namespace Nivtropy.Views
{
    public partial class DataViewControl : UserControl
    {
        private DataViewModel? _viewModel;

        public DataViewControl()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= ViewModelOnPropertyChanged;
            }

            _viewModel = e.NewValue as DataViewModel;

            if (_viewModel != null)
            {
                _viewModel.PropertyChanged += ViewModelOnPropertyChanged;
                ScrollToRequestedLine();
            }
        }

        private void ViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DataViewModel.RequestedLineHeader) || e.PropertyName == nameof(DataViewModel.Records))
            {
                ScrollToRequestedLine();
            }
        }

        private void ScrollToRequestedLine()
        {
            if (_viewModel == null || string.IsNullOrEmpty(_viewModel.RequestedLineHeader))
                return;

            var target = _viewModel.Records.FirstOrDefault(r => string.Equals(r.LineSummary?.Header, _viewModel.RequestedLineHeader, StringComparison.Ordinal));
            if (target == null)
                return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                RecordsGrid.UpdateLayout();
                RecordsGrid.SelectedItem = target;
                RecordsGrid.ScrollIntoView(target);
            }), DispatcherPriority.Background);
        }
    }
}
