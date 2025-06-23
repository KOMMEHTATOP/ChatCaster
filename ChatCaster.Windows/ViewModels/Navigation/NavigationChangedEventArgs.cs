using System;
using System.Windows.Controls;

namespace ChatCaster.Windows.ViewModels.Navigation
{
    public class NavigationChangedEventArgs : EventArgs
    {
        public string PageTag { get; }
        public Page Page { get; }

        public NavigationChangedEventArgs(string pageTag, Page page)
        {
            PageTag = pageTag;
            Page = page;
        }
    }
}
