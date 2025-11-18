using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Nivtropy.ViewModels;

namespace Nivtropy.Views
{
    public partial class TraverseCalculationView : UserControl
    {
        public TraverseCalculationView()
        {
            InitializeComponent();

            // Добавляем обработчики для зума
            this.PreviewKeyDown += TraverseCalculationView_PreviewKeyDown;
            this.PreviewMouseWheel += TraverseCalculationView_PreviewMouseWheel;
            this.Focusable = true;
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

        /// <summary>
        /// Обработка нажатий клавиш для зума (+/-)
        /// </summary>
        private void TraverseCalculationView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is TraverseCalculationViewModel viewModel)
            {
                var settings = viewModel.Settings;

                // Обработка клавиш + и =
                if ((e.Key == Key.Add || e.Key == Key.OemPlus) && settings.TableFontSize < 20)
                {
                    settings.TableFontSize += 1;
                    e.Handled = true;
                }
                // Обработка клавиши -
                else if ((e.Key == Key.Subtract || e.Key == Key.OemMinus) && settings.TableFontSize > 10)
                {
                    settings.TableFontSize -= 1;
                    e.Handled = true;
                }
            }
        }

        /// <summary>
        /// Обработка Ctrl+MouseWheel для зума
        /// </summary>
        private void TraverseCalculationView_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (DataContext is TraverseCalculationViewModel viewModel)
                {
                    var settings = viewModel.Settings;

                    if (e.Delta > 0 && settings.TableFontSize < 20)
                    {
                        settings.TableFontSize += 1;
                    }
                    else if (e.Delta < 0 && settings.TableFontSize > 10)
                    {
                        settings.TableFontSize -= 1;
                    }

                    e.Handled = true;
                }
            }
        }
    }
}
