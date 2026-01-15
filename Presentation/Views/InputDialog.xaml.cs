using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace Nivtropy.Presentation.Viewss
{
    /// <summary>
    /// Простой диалог для ввода текста
    /// </summary>
    public partial class InputDialog : Window, INotifyPropertyChanged
    {
        private string _title = "";
        private string _question = "";
        private string _responseText = "";

        public InputDialog(string title, string question, string defaultResponse = "")
        {
            InitializeComponent();
            DataContext = this;

            Title = title;
            Question = question;
            ResponseText = defaultResponse;

            Loaded += (s, e) => ResponseTextBox.SelectAll();
            ResponseTextBox.Focus();
        }

        public new string Title
        {
            get => _title;
            set
            {
                _title = value;
                base.Title = value; // Синхронизируем с Window.Title
                OnPropertyChanged();
            }
        }

        public string Question
        {
            get => _question;
            set
            {
                _question = value;
                OnPropertyChanged();
            }
        }

        public string ResponseText
        {
            get => _responseText;
            set
            {
                _responseText = value;
                OnPropertyChanged();
            }
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
