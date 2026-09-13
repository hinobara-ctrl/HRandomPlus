using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace HRandomPlus.Desktop;

public sealed class GuideWindow : Window
{
    public GuideWindow()
    {
        Title = "Guide";
        Width = 760;
        Height = 620;
        MinWidth = 600;
        MinHeight = 360;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var tabs = new TabControl
        {
            ItemsSource = GuideContent.Sections.Select(section => new TabItem
            {
                Header = section.Title,
                Content = BuildSection(section)
            }).ToArray()
        };
        var close = new Button { Content = "Close", HorizontalAlignment = HorizontalAlignment.Right };
        close.Click += (_, _) => Close();
        var layout = new Grid { RowDefinitions = new RowDefinitions("*,Auto"), Margin = new Thickness(16), RowSpacing = 12 };
        layout.Children.Add(tabs);
        Grid.SetRow(close, 1);
        layout.Children.Add(close);
        Content = layout;
        KeyDown += (_, args) =>
        {
            if (args.Key != Key.Escape) return;
            Close();
            args.Handled = true;
        };
    }

    private static ScrollViewer BuildSection(GuideSection section)
    {
        var panel = new StackPanel { Spacing = 12, Margin = new Thickness(8, 16, 16, 16) };
        panel.Children.Add(Paragraph(section.Introduction));
        foreach (GuideEntry entry in section.Entries)
        {
            var title = Paragraph(entry.Title);
            title.FontSize = 17;
            title.FontWeight = FontWeight.SemiBold;
            title.Margin = new Thickness(0, 8, 0, 0);
            panel.Children.Add(title);
            panel.Children.Add(Paragraph(entry.Description));
        }
        return new ScrollViewer
        {
            Content = panel,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
        };
    }

    private static TextBlock Paragraph(string text) => new()
    {
        Text = text,
        FontSize = 14,
        TextWrapping = TextWrapping.Wrap
    };
}
