using System.Windows;
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
        
        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                Log.Debug("MainPageView выгружен (cleanup ViewModel управляется NavigationManager)");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при выгрузке MainPageView");
            }
        }
    }
}
