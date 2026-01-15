using System.Windows;
using Nivtropy.Presentation.Viewss;

namespace Nivtropy.Presentation.Services
{
    /// <summary>
    /// Реализация сервиса диалогов через WPF MessageBox и InputDialog
    /// </summary>
    public class DialogService : IDialogService
    {
        public void ShowInfo(string message, string title = "Информация")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public void ShowError(string message, string title = "Ошибка")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public void ShowWarning(string message, string title = "Предупреждение")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        public bool Confirm(string message, string title = "Подтверждение")
        {
            var result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
            return result == MessageBoxResult.Yes;
        }

        public string? PromptInput(string message, string title = "Ввод", string? defaultValue = null)
        {
            var dialog = new InputDialog(message, title, defaultValue);
            return dialog.ShowDialog() == true ? dialog.ResponseText : null;
        }
    }
}
