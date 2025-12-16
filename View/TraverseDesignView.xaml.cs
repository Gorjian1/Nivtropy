using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Controls;
using Nivtropy.Services.Visualization;
using Nivtropy.ViewModels;

namespace Nivtropy.Views
{
    public partial class TraverseDesignView : UserControl
    {
        private readonly GeneratedProfileVisualizationService _visualizationService = new();

        private DataGeneratorViewModel? ViewModel => DataContext as DataGeneratorViewModel;

        public TraverseDesignView()
        {
            InitializeComponent();

            DataContextChanged += TraverseDesignView_DataContextChanged;
        }

        private void TraverseDesignView_DataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is DataGeneratorViewModel oldVm)
            {
                oldVm.Measurements.CollectionChanged -= Measurements_CollectionChanged;
                oldVm.PropertyChanged -= ViewModel_PropertyChanged;
            }

            if (e.NewValue is DataGeneratorViewModel newVm)
            {
                newVm.Measurements.CollectionChanged += Measurements_CollectionChanged;
                newVm.PropertyChanged += ViewModel_PropertyChanged;
            }

            RedrawProfile();
        }

        private void Measurements_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RedrawProfile();
        }

        private void GeneratedProfileCanvas_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
        {
            RedrawProfile();
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DataGeneratorViewModel.SelectedLineName))
            {
                RedrawProfile();
            }
        }

        private void RedrawProfile()
        {
            if (ViewModel == null)
                return;

            var selectedLine = ViewModel.SelectedLineName;
            var measurements = string.IsNullOrWhiteSpace(selectedLine)
                ? ViewModel.Measurements.ToList()
                : ViewModel.Measurements.Where(m => m.LineName == selectedLine).ToList();

            _visualizationService.DrawProfile(GeneratedProfileCanvas, measurements);
        }
    }
}
