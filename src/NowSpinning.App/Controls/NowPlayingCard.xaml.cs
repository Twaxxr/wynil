using System.Windows.Controls;

namespace NowSpinning.App.Controls;

public partial class NowPlayingCard : System.Windows.Controls.UserControl, IDisposable
{
    public NowPlayingCard() => InitializeComponent();

    public void Dispose()
    {
        Preview.Dispose();
        GC.SuppressFinalize(this);
    }
}
