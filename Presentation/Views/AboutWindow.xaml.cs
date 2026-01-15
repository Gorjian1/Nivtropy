using System;
using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace Nivtropy.Presentation.Viewss
{
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
            LoadMarkdownContent();
        }

        private void LoadMarkdownContent()
        {
            try
            {
                // Путь к MD файлу
                var mdPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "About.md");

                if (!File.Exists(mdPath))
                {
                    AboutDocument.Blocks.Add(new Paragraph(new Run("Файл About.md не найден."))
                    {
                        Foreground = Brushes.Red
                    });
                    return;
                }

                var markdown = File.ReadAllText(mdPath);
                ConvertMarkdownToFlowDocument(markdown);
            }
            catch (Exception ex)
            {
                AboutDocument.Blocks.Add(new Paragraph(new Run($"Ошибка загрузки: {ex.Message}"))
                {
                    Foreground = Brushes.Red
                });
            }
        }

        private void ConvertMarkdownToFlowDocument(string markdown)
        {
            var lines = markdown.Split('\n');
            Paragraph? currentParagraph = null;
            List? currentList = null;

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                // Пустая строка
                if (string.IsNullOrWhiteSpace(trimmedLine))
                {
                    if (currentParagraph != null && currentParagraph.Inlines.Count > 0)
                    {
                        AboutDocument.Blocks.Add(currentParagraph);
                        currentParagraph = null;
                    }
                    if (currentList != null)
                    {
                        AboutDocument.Blocks.Add(currentList);
                        currentList = null;
                    }
                    continue;
                }

                // Горизонтальная линия
                if (trimmedLine == "---")
                {
                    if (currentParagraph != null)
                    {
                        AboutDocument.Blocks.Add(currentParagraph);
                        currentParagraph = null;
                    }
                    if (currentList != null)
                    {
                        AboutDocument.Blocks.Add(currentList);
                        currentList = null;
                    }
                    var separator = new Paragraph(new Run(""))
                    {
                        BorderBrush = Brushes.LightGray,
                        BorderThickness = new Thickness(0, 1, 0, 0),
                        Margin = new Thickness(0, 10, 0, 10)
                    };
                    AboutDocument.Blocks.Add(separator);
                    continue;
                }

                // Заголовок H1
                if (trimmedLine.StartsWith("# "))
                {
                    if (currentParagraph != null) AboutDocument.Blocks.Add(currentParagraph);
                    if (currentList != null) AboutDocument.Blocks.Add(currentList);
                    currentList = null;

                    currentParagraph = new Paragraph(new Run(trimmedLine.Substring(2)))
                    {
                        FontSize = 24,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4)),
                        Margin = new Thickness(0, 10, 0, 10)
                    };
                    AboutDocument.Blocks.Add(currentParagraph);
                    currentParagraph = null;
                    continue;
                }

                // Заголовок H2
                if (trimmedLine.StartsWith("## "))
                {
                    if (currentParagraph != null) AboutDocument.Blocks.Add(currentParagraph);
                    if (currentList != null) AboutDocument.Blocks.Add(currentList);
                    currentList = null;

                    currentParagraph = new Paragraph(new Run(trimmedLine.Substring(3)))
                    {
                        FontSize = 18,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)),
                        Margin = new Thickness(0, 12, 0, 8)
                    };
                    AboutDocument.Blocks.Add(currentParagraph);
                    currentParagraph = null;
                    continue;
                }

                // Заголовок H3
                if (trimmedLine.StartsWith("### "))
                {
                    if (currentParagraph != null) AboutDocument.Blocks.Add(currentParagraph);
                    if (currentList != null) AboutDocument.Blocks.Add(currentList);
                    currentList = null;

                    currentParagraph = new Paragraph(new Run(trimmedLine.Substring(4)))
                    {
                        FontSize = 15,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
                        Margin = new Thickness(0, 10, 0, 6)
                    };
                    AboutDocument.Blocks.Add(currentParagraph);
                    currentParagraph = null;
                    continue;
                }

                // Заголовок H4
                if (trimmedLine.StartsWith("#### "))
                {
                    if (currentParagraph != null) AboutDocument.Blocks.Add(currentParagraph);
                    if (currentList != null) AboutDocument.Blocks.Add(currentList);
                    currentList = null;

                    currentParagraph = new Paragraph(new Run(trimmedLine.Substring(5)))
                    {
                        FontSize = 14,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)),
                        Margin = new Thickness(0, 8, 0, 4)
                    };
                    AboutDocument.Blocks.Add(currentParagraph);
                    currentParagraph = null;
                    continue;
                }

                // Маркированный список
                if (trimmedLine.StartsWith("- "))
                {
                    if (currentParagraph != null)
                    {
                        AboutDocument.Blocks.Add(currentParagraph);
                        currentParagraph = null;
                    }
                    if (currentList == null)
                    {
                        currentList = new List
                        {
                            MarkerStyle = TextMarkerStyle.Disc,
                            Margin = new Thickness(20, 0, 0, 10)
                        };
                    }

                    var listItem = new ListItem(new Paragraph(ParseInlineFormatting(trimmedLine.Substring(2))));
                    currentList.ListItems.Add(listItem);
                    continue;
                }

                // Обычный текст
                if (currentList != null)
                {
                    AboutDocument.Blocks.Add(currentList);
                    currentList = null;
                }

                if (currentParagraph == null)
                {
                    currentParagraph = new Paragraph
                    {
                        Margin = new Thickness(0, 0, 0, 8),
                        TextAlignment = TextAlignment.Left
                    };
                }

                currentParagraph.Inlines.Add(ParseInlineFormatting(trimmedLine));
                currentParagraph.Inlines.Add(new LineBreak());
            }

            // Добавляем последний блок
            if (currentParagraph != null)
            {
                AboutDocument.Blocks.Add(currentParagraph);
            }
            if (currentList != null)
            {
                AboutDocument.Blocks.Add(currentList);
            }
        }

        private Span ParseInlineFormatting(string text)
        {
            var span = new Span();
            var parts = text.Split(new[] { "**" }, StringSplitOptions.None);

            for (int i = 0; i < parts.Length; i++)
            {
                if (i % 2 == 0)
                {
                    // Обычный текст
                    span.Inlines.Add(new Run(parts[i]));
                }
                else
                {
                    // Жирный текст
                    span.Inlines.Add(new Run(parts[i]) { FontWeight = FontWeights.Bold });
                }
            }

            return span;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
