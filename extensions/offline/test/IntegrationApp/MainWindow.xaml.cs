using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.WindowsAzure.MobileServices.TestFramework;

namespace IntegrationApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, ITestReporter
    {
        private ObservableCollection<GroupDescription> groups;
        private ObservableCollection<TestDescription> tests;
        private GroupDescription currentGroup = null;
        private TestDescription currentTest = null;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void RunTests_Click(object sender, RoutedEventArgs e)
        {
            App.Harness.Reporter = this;
            Task.Factory.StartNew(() => App.Harness.RunAsync());
        }

        public void StartRun(TestHarness harness)
        {
            throw new NotImplementedException();
        }

        public void Progress(TestHarness harness)
        {
            throw new NotImplementedException();
        }

        public void EndRun(TestHarness harness)
        {
            throw new NotImplementedException();
        }

        public void StartGroup(TestGroup group)
        {
            throw new NotImplementedException();
        }

        public void EndGroup(TestGroup group)
        {
            throw new NotImplementedException();
        }

        public void StartTest(TestMethod test)
        {
            throw new NotImplementedException();
        }

        public async void EndTest(TestMethod test)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                if (test.Excluded)
                {
                    currentTest.Color = Color.FromArgb(0xFF, 0x66, 0x66, 0x66);
                }
                else if (!test.Passed)
                {
                    currentTest.Color = Color.FromArgb(0xFF, 0xFF, 0x00, 0x6E);
                }
                else
                {
                    currentTest.Color = Color.FromArgb(0xFF, 0x2A, 0x9E, 0x39);
                }
                currentTest = null;
            });
        }

        public void Log(string message)
        {
            throw new NotImplementedException();
        }

        public void Error(string errorDetails)
        {
            throw new NotImplementedException();
        }

        public void Status(string status)
        {
            throw new NotImplementedException();
        }
    }
}
