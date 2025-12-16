using System.Collections.Specialized;
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
            }

            if (e.NewValue is DataGeneratorViewModel newVm)
            {
                newVm.Measurements.CollectionChanged += Measurements_CollectionChanged;
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

        private void RedrawProfile()
        {
            if (ViewModel == null)
                return;

            _visualizationService.DrawProfile(GeneratedProfileCanvas, ViewModel.Measurements);
        }
    }
}
