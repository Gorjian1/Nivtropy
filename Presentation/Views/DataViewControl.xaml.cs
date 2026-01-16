using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Nivtropy.Models;
using Nivtropy.Presentation.Models;
using Nivtropy.Presentation.ViewModels;

namespace Nivtropy.Presentation.Views
{
    public partial class DataViewControl : UserControl
    {
        private DataViewModel? _viewModel;
        private DataGrid? _recordsGrid;

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

        private DataGrid? EnsureRecordsGrid()
        {
            if (_recordsGrid == null)
            {
                _recordsGrid = FindName("RecordsGrid") as DataGrid;
            }

            return _recordsGrid;
        }

        private void ScrollToSelectedRun()
        {
            if (_viewModel?.SelectedRun is not LineSummary run)
                return;

            var targetRecord = _viewModel.Records.FirstOrDefault(r => ReferenceEquals(r.LineSummary, run));
            if (targetRecord == null)
                return;

            var recordsGrid = EnsureRecordsGrid();
            if (recordsGrid == null)
                return;

            if (!Equals(recordsGrid.SelectedItem, targetRecord))
            {
                recordsGrid.SelectedItem = targetRecord;
            }

            recordsGrid.UpdateLayout();
            recordsGrid.ScrollIntoView(targetRecord);
        }
    }
}
