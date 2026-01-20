using System.Windows.Controls;
using Nivtropy.Presentation.ViewModels;

namespace Nivtropy.Presentation.Visualization
{
    /// <summary>
    /// Интерфейс сервиса для визуализации графа связей между ходами нивелирной сети
    /// </summary>
    public interface ITraverseSystemVisualizationService
    {
        /// <summary>
        /// Рисует визуализацию системы ходов с графом связей
        /// </summary>
        /// <param name="canvas">Canvas для рисования</param>
        /// <param name="viewModel">ViewModel с данными о ходах</param>
        void DrawSystemVisualization(Canvas canvas, TraverseJournalViewModel viewModel);
    }
}
