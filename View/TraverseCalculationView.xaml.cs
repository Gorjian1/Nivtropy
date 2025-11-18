using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Nivtropy.Views
{
    public partial class TraverseCalculationView : UserControl
    {
        public TraverseCalculationView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Показать детали хода (всплывающее окно)
        /// </summary>
        private void ShowTraverseDetails_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string traverseName)
            {
                PopupTitle.Text = $"Детали хода: {traverseName}";
                TraverseDetailsPopup.Visibility = Visibility.Visible;

                // TODO: здесь можно добавить отрисовку профиля хода
                // DrawTraverseProfile(traverseName);
            }
        }

        /// <summary>
        /// Закрыть popup (кнопка ✕)
        /// </summary>
        private void ClosePopup_Click(object sender, RoutedEventArgs e)
        {
            TraverseDetailsPopup.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Закрыть popup при клике на затемненный фон
        /// </summary>
        private void TraverseDetailsPopup_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            TraverseDetailsPopup.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Предотвратить закрытие popup при клике на само окно
        /// </summary>
        private void PopupContent_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true; // Остановить всплытие события
        }
    }
}
