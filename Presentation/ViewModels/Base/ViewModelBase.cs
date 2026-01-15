using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Nivtropy.Presentation.ViewModels.Base
{
    /// <summary>
    /// Базовый класс для всех ViewModel с общей логикой:
    /// - INotifyPropertyChanged
    /// - Batch update handling
    /// - SetField helper
    /// </summary>
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        private bool _isUpdating;

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Флаг, указывающий что идёт массовое обновление данных
        /// </summary>
        protected bool IsUpdating => _isUpdating;

        /// <summary>
        /// Вызывает PropertyChanged для указанного свойства
        /// </summary>
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Устанавливает значение поля и вызывает PropertyChanged если значение изменилось
        /// </summary>
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        /// <summary>
        /// Подписывается на события batch update от DataViewModel
        /// </summary>
        protected void SubscribeToBatchUpdates(DataViewModel dataViewModel)
        {
            if (dataViewModel == null)
                return;

            dataViewModel.BeginBatchUpdate += OnBeginBatchUpdate;
            dataViewModel.EndBatchUpdate += OnEndBatchUpdate;
        }

        /// <summary>
        /// Отписывается от событий batch update
        /// </summary>
        protected void UnsubscribeFromBatchUpdates(DataViewModel dataViewModel)
        {
            if (dataViewModel == null)
                return;

            dataViewModel.BeginBatchUpdate -= OnBeginBatchUpdate;
            dataViewModel.EndBatchUpdate -= OnEndBatchUpdate;
        }

        private void OnBeginBatchUpdate(object? sender, EventArgs e)
        {
            _isUpdating = true;
        }

        private void OnEndBatchUpdate(object? sender, EventArgs e)
        {
            _isUpdating = false;
            OnBatchUpdateCompleted();
        }

        /// <summary>
        /// Вызывается после завершения batch update.
        /// Переопределите для выполнения обновления данных.
        /// </summary>
        protected virtual void OnBatchUpdateCompleted()
        {
            // По умолчанию ничего не делает
            // Наследники могут переопределить для вызова UpdateRows() и т.п.
        }
    }
}
