namespace Nivtropy.Presentation.Services.Dialog
{
    /// <summary>
    /// Интерфейс сервиса диалогов
    /// Абстрагирует UI-диалоги для тестируемости
    /// </summary>
    public interface IDialogService
    {
        /// <summary>
        /// Показывает информационное сообщение
        /// </summary>
        void ShowInfo(string message, string title = "Информация");

        /// <summary>
        /// Показывает сообщение об ошибке
        /// </summary>
        void ShowError(string message, string title = "Ошибка");

        /// <summary>
        /// Показывает предупреждение
        /// </summary>
        void ShowWarning(string message, string title = "Предупреждение");

        /// <summary>
        /// Показывает диалог подтверждения
        /// </summary>
        /// <returns>true если пользователь подтвердил, false иначе</returns>
        bool Confirm(string message, string title = "Подтверждение");

        /// <summary>
        /// Показывает диалог ввода текста
        /// </summary>
        /// <returns>Введённый текст или null при отмене</returns>
        string? PromptInput(string message, string title = "Ввод", string? defaultValue = null);
    }
}
