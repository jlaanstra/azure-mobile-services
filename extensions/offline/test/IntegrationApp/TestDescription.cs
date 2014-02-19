// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ----------------------------------------------------------------------------

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

#if NETFX_CORE
using Windows.UI;
using Windows.UI.Xaml.Media;
#endif

#if !NETFX_CORE
using System.Windows.Media;
using System;
#endif

namespace Microsoft.WindowsAzure.MobileServices.Test
{
    /// <summary>
    /// UI model for a test method.
    /// </summary>
    public class TestDescription : INotifyPropertyChanged
    {
        private string name;        
        public string Name
        {
            get { return name; }
            set
            {
                name = value;
                OnPropertyChanged();
            }
        }

        private Brush brush;
        public Brush Brush
        {
            get { return brush; }
            set
            {
                brush = value;
                OnPropertyChanged();
            }
        }

        private TimeSpan duration;
        public TimeSpan Duration
        {
            get { return duration; }
            set
            {
                duration = value;
                OnPropertyChanged();
            }
        }

        private long bytes;
        public long Bytes
        {
            get { return bytes; }
            set
            {
                bytes = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<string> Details { get; private set; }

        public TestDescription()
        {
            Details = new ObservableCollection<string>();
            Brush = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string memberName = "")
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(memberName));
            }
        }
    }
}
