using Serilog;

namespace ChatCaster.Windows.Views.ViewSettings
{
    public partial class MainPageView
    {
        public MainPageView()
        {
            InitializeComponent();
            Log.Debug("MainPageView создан (ViewModel будет установлен извне)");
        }
    }
}
