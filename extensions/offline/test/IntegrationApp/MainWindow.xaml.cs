using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
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
using Microsoft.Win32;
using Microsoft.WindowsAzure.MobileServices.Test;
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

        private NetworkTraffic traffic;
        private Stopwatch stopwatch;
        private long bytesStart;

        public MainWindow()
        {
            InitializeComponent();

            string mobileServiceOfflineRuntimeURL = Settings.Default.MobileServiceOfflineRuntimeUrl;
            string mobileServiceOfflineRuntimeKey = Settings.Default.MobileServiceOfflineRuntimeKey;
            string mobileServiceNormalRuntimeURL = Settings.Default.MobileServiceNormalRuntimeUrl;
            string mobileServiceNormalRuntimeKey = Settings.Default.MobileServiceNormalRuntimeKey;
            string tagExpression = Settings.Default.TagExpression;

            this.syncService.Text = mobileServiceOfflineRuntimeURL ?? string.Empty;
            this.syncServiceKey.Text = mobileServiceOfflineRuntimeKey ?? string.Empty;
            this.noSyncService.Text = mobileServiceNormalRuntimeURL ?? string.Empty;
            this.noSyncServiceKey.Text = mobileServiceNormalRuntimeKey ?? string.Empty;
            this.tag.Text = tagExpression ?? string.Empty;

            // Setup the groups data source
            groups = new ObservableCollection<GroupDescription>();
            tests = new ObservableCollection<TestDescription>();
            CollectionViewSource src = (CollectionViewSource)this.Resources["cvsTests"];
            src.Source = tests;

            this.traffic = new NetworkTraffic(Process.GetCurrentProcess().Id);
            this.stopwatch = new Stopwatch();

            Loaded += (s, e) => this.runTests.Focus();
        }

        private void RunTests_Click(object sender, RoutedEventArgs e)
        {
            App.Harness.Reporter = this;

            App.Harness.Settings.Custom["MobileServiceOfflineRuntimeUrl"] = this.syncService.Text;
            App.Harness.Settings.Custom["MobileServiceOfflineRuntimeKey"] = this.syncServiceKey.Text;
            App.Harness.Settings.Custom["MobileServiceNormalRuntimeUrl"] = this.noSyncService.Text;
            App.Harness.Settings.Custom["MobileServiceNormalRuntimeKey"] = this.noSyncServiceKey.Text;
            App.Harness.Settings.TagExpression = 
                string.IsNullOrEmpty(this.tag.Text) ? "All - ResponseTime - BandwidthUsage" : this.tag.Text;

            Settings.Default.MobileServiceOfflineRuntimeUrl = this.syncService.Text;
            Settings.Default.MobileServiceOfflineRuntimeKey = this.syncServiceKey.Text;
            Settings.Default.MobileServiceNormalRuntimeUrl = this.noSyncService.Text;
            Settings.Default.MobileServiceNormalRuntimeKey = this.noSyncServiceKey.Text;
            Settings.Default.TagExpression = this.tag.Text;

            Settings.Default.Save();

            Task.Factory.StartNew(() => App.Harness.RunAsync());
        }

        private void SaveResults_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog()
            {
                Title = "Choose file to save to",
                FileName = "example.csv",
                Filter = "CSV (*.csv)|*.csv",
                FilterIndex = 0,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            };

            if(sfd.ShowDialog() == true)
            {
                using(Stream file = File.OpenWrite(sfd.FileName))
                {
                    using(StreamWriter writer = new StreamWriter(file))
                    {
                        foreach(TestDescription t in this.tests)
                        {
                            writer.WriteLine(string.Format("{0},{1},{2}", t.Name, t.Duration.TotalMilliseconds, t.Bytes));
                        }
                    }
                }
            }
        }

        public async void StartRun(TestHarness harness)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                lblCurrentTestNumber.Text = harness.Progress.ToString();
                lblTotalTestNumber.Text = harness.Count.ToString();
                lblFailureNumber.Tag = harness.Failures.ToString() ?? "0";
                progress.Value = 1;
            });
        }

        public async void Progress(TestHarness harness)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                lblCurrentTestNumber.Text = harness.Progress.ToString();
                lblFailureNumber.Text = " " + (harness.Failures.ToString() ?? "0");
                double value = harness.Progress;
                int count = harness.Count;
                if (count > 0)
                {
                    value = value * 100.0 / (double)count;
                }
                progress.Value = value;
            });
        }

        public void EndRun(TestHarness harness)
        {
            
        }

        public async void StartGroup(TestGroup group)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                currentGroup = new GroupDescription { Name = group.Name };
                groups.Add(currentGroup);
            });
        }

        public async void EndGroup(TestGroup group)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                currentGroup = null;
            });
        }

        public async void StartTest(TestMethod test)
        {

            this.bytesStart = this.traffic.GetBytesSent() + this.traffic.GetBytesReceived();
            this.stopwatch.Reset();
            this.stopwatch.Start();

            await Dispatcher.InvokeAsync(async () =>
            {
                currentTest = new TestDescription { Name = test.Name };
                currentGroup.Add(currentTest);
                tests.Add(currentTest);

                await Dispatcher.InvokeAsync(() =>
                {
                    lstTests.ScrollIntoView(currentTest);
                });
            });
        }

        public async void EndTest(TestMethod test)
        {
            long bytesEnd = this.traffic.GetBytesSent() + this.traffic.GetBytesReceived() - bytesStart;
            this.stopwatch.Stop();
            TimeSpan time = this.stopwatch.Elapsed;

            await Dispatcher.InvokeAsync(() =>
            {
                currentTest.Duration = time;
                currentTest.Bytes = bytesEnd;
                if (test.Excluded)
                {
                    currentTest.Brush = new SolidColorBrush(Color.FromArgb(0xFF, 0x66, 0x66, 0x66));
                }
                else if (!test.Passed)
                {
                    currentTest.Brush = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0x00, 0x6E));
                }
                else
                {
                    currentTest.Brush = new SolidColorBrush(Color.FromArgb(0xFF, 0x2A, 0x9E, 0x39));
                }
                currentTest = null;
            });
        }

        public async void Log(string message)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                if (currentTest != null)
                {
                    currentTest.Details.Add(message);
                }
            });
        }

        public async void Error(string errorDetails)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                if (currentTest != null)
                {
                    currentTest.Details.Add(errorDetails);
                }
            });
        }

        public void Status(string status)
        {
        }

        private void lstTests_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            TestDescription test = e.AddedItems.OfType<TestDescription>().FirstOrDefault();
            if (test != null)
            {
                this.details.Text = string.Join("\n\r", test.Details);
            }
            else
            {
                this.details.Text = "No test selected.";
            }
        }
    }
}
