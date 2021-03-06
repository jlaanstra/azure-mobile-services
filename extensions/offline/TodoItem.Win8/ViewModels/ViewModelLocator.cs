/*
  In App.xaml:
  <Application.Resources>
      <vm:ViewModelLocator xmlns:vm="clr-namespace:Demo"
                           x:Key="Locator" />
  </Application.Resources>
  
  In the View:
  DataContext="{Binding Source={StaticResource Locator}, Path=ViewModelName}"

  You can also use Blend to do all this with the tool's support.
  See http://www.galasoft.ch/mvvm
*/

using System;
using System.Net.Http;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Ioc;
using Microsoft.Practices.ServiceLocation;
using Microsoft.WindowsAzure.MobileServices;
using Microsoft.WindowsAzure.MobileServices.Caching;

namespace Todo.ViewModels
{
    /// <summary>
    /// This class contains static references to all the view models in the
    /// application and provides an entry point for the bindings.
    /// </summary>
    public class ViewModelLocator
    {
        /// <summary>
        /// Initializes a new instance of the ViewModelLocator class.
        /// </summary>
        public ViewModelLocator()
        {
            ServiceLocator.SetLocatorProvider(() => SimpleIoc.Default);

            ToggableNetworkInformation networkInfo = new ToggableNetworkInformation();
            SimpleIoc.Default.Register<INetworkInformation>(() => networkInfo);            
            SimpleIoc.Default.Register<NetworkInformationDelegate>(() =>
            {
                return new NetworkInformationDelegate(() => networkInfo.IsOnline, b => networkInfo.IsOnline = b);
            });

            SimpleIoc.Default.Register<MainViewModel>();

            // Configure your mobile service
            MobileServiceClient MobileService = new MobileServiceClient(
                Constants.MobileServiceUrl,
                Constants.MobileServiceKey
            );
            MobileService = MobileService.UseOfflineDataCapabilitiesForTables(
                SimpleIoc.Default.GetInstance<INetworkInformation>(), new DialogConflictResolver());

            SimpleIoc.Default.Register<IMobileServiceClient>(() => MobileService);
        }

        public IMobileServiceClient Client
        {
            get
            {
                return ServiceLocator.Current.GetInstance<IMobileServiceClient>();
            }
        }

        public MainViewModel MainViewModel
        {
            get
            {
                return ServiceLocator.Current.GetInstance<MainViewModel>();
            }
        }

        public static void Cleanup()
        {
            // TODO Clear the ViewModels
        }
    }
}