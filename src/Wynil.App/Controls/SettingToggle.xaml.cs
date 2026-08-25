using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NowSpinning.App.Controls;

public partial class SettingToggle : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(SettingToggle));
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(nameof(Description), typeof(string), typeof(SettingToggle), new PropertyMetadata(string.Empty, OnDescriptionChanged));
    public static readonly DependencyProperty IconDataProperty = DependencyProperty.Register(nameof(IconData), typeof(Geometry), typeof(SettingToggle));
    public static readonly DependencyProperty IsCheckedProperty = DependencyProperty.Register(nameof(IsChecked), typeof(bool), typeof(SettingToggle), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
    public static readonly DependencyProperty HasDescriptionProperty = DependencyProperty.Register(nameof(HasDescription), typeof(bool), typeof(SettingToggle));

    public SettingToggle() => InitializeComponent();

    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Description { get => (string)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public Geometry IconData { get => (Geometry)GetValue(IconDataProperty); set => SetValue(IconDataProperty, value); }
    public bool IsChecked { get => (bool)GetValue(IsCheckedProperty); set => SetValue(IsCheckedProperty, value); }
    public bool HasDescription { get => (bool)GetValue(HasDescriptionProperty); private set => SetValue(HasDescriptionProperty, value); }

    private static void OnDescriptionChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        ((SettingToggle)sender).HasDescription = !string.IsNullOrWhiteSpace(args.NewValue as string);
}
