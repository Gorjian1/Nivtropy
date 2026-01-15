using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Nivtropy.Models;
using Nivtropy.Presentation.ViewModelss;

namespace Nivtropy.Presentation.Viewss
{
    /// <summary>
    /// Окно управления системами ходов
    /// </summary>
    public partial class SystemsManagementWindow : Window
    {
        private readonly TraverseCalculationViewModel _calculation;

        public SystemsManagementWindow(TraverseCalculationViewModel calculation)
        {
            InitializeComponent();
            _calculation = calculation;
            LoadSystems();
        }

        /// <summary>
        /// Загружает список систем
        /// </summary>
        private void LoadSystems()
        {
            SystemsListBox.ItemsSource = _calculation.Systems;
            if (_calculation.Systems.Count > 0)
            {
                SystemsListBox.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// Обработчик выбора системы
        /// </summary>
        private void SystemsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SystemsListBox.SelectedItem is TraverseSystem selectedSystem)
            {
                RunsHeaderText.Text = $"Ходы системы: {selectedSystem.Name}";

                // Показываем ходы выбранной системы
                var runsInSystem = _calculation.Runs
                    .Where(r => r.SystemId == selectedSystem.Id)
                    .ToList();

                RunsListBox.ItemsSource = runsInSystem;

                // Заполняем ComboBox для перемещения (все системы кроме текущей)
                MoveToSystemComboBox.ItemsSource = _calculation.Systems
                    .Where(s => s.Id != selectedSystem.Id)
                    .ToList();

                if (MoveToSystemComboBox.Items.Count > 0)
                {
                    MoveToSystemComboBox.SelectedIndex = 0;
                }
            }
        }

        /// <summary>
        /// Создание новой системы
        /// </summary>
        private void CreateSystem_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new InputDialog("Создание системы", "Введите название системы:");
            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ResponseText))
            {
                var newSystem = new TraverseSystem(
                    System.Guid.NewGuid().ToString(),
                    dialog.ResponseText,
                    _calculation.Systems.Count
                );

                _calculation.Systems.Add(newSystem);
                SystemsListBox.SelectedItem = newSystem;
            }
        }

        /// <summary>
        /// Переименование системы
        /// </summary>
        private void RenameSystem_Click(object sender, RoutedEventArgs e)
        {
            if (SystemsListBox.SelectedItem is TraverseSystem selectedSystem)
            {
                // Нельзя переименовать систему по умолчанию
                if (selectedSystem.Id == "system-default")
                {
                    MessageBox.Show(
                        "Систему по умолчанию нельзя переименовать.",
                        "Ошибка",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                var dialog = new InputDialog("Переименование системы", "Введите новое название:", selectedSystem.Name);
                if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ResponseText))
                {
                    selectedSystem.Name = dialog.ResponseText;

                    // Обновляем отображение
                    SystemsListBox.Items.Refresh();
                    SystemsListBox_SelectionChanged(SystemsListBox, null!);
                }
            }
        }

        /// <summary>
        /// Удаление системы
        /// </summary>
        private void DeleteSystem_Click(object sender, RoutedEventArgs e)
        {
            if (SystemsListBox.SelectedItem is TraverseSystem selectedSystem)
            {
                // Нельзя удалить систему по умолчанию
                if (selectedSystem.Id == "system-default")
                {
                    MessageBox.Show(
                        "Систему по умолчанию нельзя удалить.",
                        "Ошибка",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                var result = MessageBox.Show(
                    $"Удалить систему '{selectedSystem.Name}'?\n\nВсе ходы будут перемещены в систему по умолчанию.",
                    "Подтверждение удаления",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    var defaultSystem = _calculation.Systems.FirstOrDefault(s => s.Id == "system-default");
                    if (defaultSystem == null)
                        return;

                    // Перемещаем все ходы в систему по умолчанию
                    foreach (var run in _calculation.Runs.Where(r => r.SystemId == selectedSystem.Id))
                    {
                        run.SystemId = defaultSystem.Id;
                        selectedSystem.RemoveRun(run.Index);
                        defaultSystem.AddRun(run.Index);
                    }

                    _calculation.Systems.Remove(selectedSystem);

                    // Выбираем систему по умолчанию
                    SystemsListBox.SelectedItem = defaultSystem;
                }
            }
        }

        /// <summary>
        /// Перемещение выбранных ходов в другую систему
        /// </summary>
        private void MoveRuns_Click(object sender, RoutedEventArgs e)
        {
            if (MoveToSystemComboBox.SelectedItem is not TraverseSystem targetSystem)
            {
                MessageBox.Show(
                    "Выберите целевую систему для перемещения.",
                    "Внимание",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            if (RunsListBox.SelectedItems.Count == 0)
            {
                MessageBox.Show(
                    "Выберите ходы для перемещения.",
                    "Внимание",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var currentSystem = SystemsListBox.SelectedItem as TraverseSystem;
            if (currentSystem == null)
                return;

            var selectedRuns = RunsListBox.SelectedItems.Cast<LineSummary>().ToList();

            foreach (var run in selectedRuns)
            {
                // Удаляем из текущей системы
                currentSystem.RemoveRun(run.Index);

                // Добавляем в целевую систему
                targetSystem.AddRun(run.Index);
                run.SystemId = targetSystem.Id;
            }

            // Обновляем отображение
            SystemsListBox.Items.Refresh();
            SystemsListBox_SelectionChanged(SystemsListBox, null!);

            MessageBox.Show(
                $"Перемещено ходов: {selectedRuns.Count}",
                "Успешно",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        /// <summary>
        /// Закрытие окна
        /// </summary>
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
