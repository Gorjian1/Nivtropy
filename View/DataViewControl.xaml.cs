using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Nivtropy.Models;
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
                _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            }

            _viewModel = e.NewValue as DataViewModel;

            if (_viewModel != null)
            {
                _viewModel.PropertyChanged += OnViewModelPropertyChanged;
                Dispatcher.InvokeAsync(ScrollToSelectedRun, DispatcherPriority.Background);
            }
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DataViewModel.SelectedRun))
            {
                Dispatcher.InvokeAsync(ScrollToSelectedRun, DispatcherPriority.Background);
            }
        }

        private void ScrollToSelectedRun()
        {
            if (_viewModel?.SelectedRun is not LineSummary run)
                return;

            var targetRecord = _viewModel.Records.FirstOrDefault(r => ReferenceEquals(r.LineSummary, run));
            if (targetRecord == null)
                return;

            if (!Equals(RecordsGrid.SelectedItem, targetRecord))
            {
                RecordsGrid.SelectedItem = targetRecord;
            }

            RecordsGrid.UpdateLayout();
            RecordsGrid.ScrollIntoView(targetRecord);
        }
    }
}
