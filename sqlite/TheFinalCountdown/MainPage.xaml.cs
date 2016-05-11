#define DEBUG

/* 
    Copyright (c) 2012 Microsoft Corporation.  All rights reserved.
    Use of this sample source code is subject to the terms of the Microsoft license 
    agreement under which you licensed this sample source code and is provided AS-IS.
    If you did not accept the terms of the license agreement, you are not authorized 
    to use this sample source code.  For the terms of the license, please see the 
    license agreement between you and Microsoft.
  
    To see all Code Samples for Windows Phone, visit http://go.microsoft.com/fwlink/?LinkID=219604 
  
*/

using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using Windows.Phone.System.UserProfile;
using Windows.System;
using Coding4Fun.Phone.Controls;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Notification;
using Microsoft.Phone.Scheduler;
using Microsoft.Phone.Shell;
using Microsoft.Phone.Tasks;

namespace TheFinalCountdown
{
    public partial class MainPage : PhoneApplicationPage
    {
        private static PhotoChooserTask _photoChooserTask;
        private readonly TileHelper _tileHelper = new TileHelper();
        private bool _agentsAreEnabled = true;

        // Variable to hold our lock screen provider status
        private bool _isLockScreenProvider;

        // Variables for our periodic task to update the lock screen
        private PeriodicTask _periodicTask;
        private const string PeriodicTaskName = "PeriodicAgent";


        // Push Uri for this application.
        private Uri _pushChannelUri;
        private ShellTileSchedule _shellTileSchedule;

        // Constructor
        public MainPage()
        {
            InitializeComponent();

            Loaded += MainPage_Loaded;


            // Initiailize the ShellTileSchedule (but don't start it yet)

            var tileData = new IconicTileData
            {
                Title = "Scheduled update",
                // TODO: OR USE A REMOTE IMAGE URI WHICH CAN BE UPDATED SEVER-SIDE AND DOWNLOADED AS THE TILE IMAGE
                SmallIconImage = new Uri("http://pushtestserver.blob.core.windows.net/tiles/update_iconic_smalliconimage.png", UriKind.Absolute),
                IconImage = new Uri("http://pushtestserver.blob.core.windows.net/tiles/update_iconic_iconimage.png", UriKind.Absolute),
                BackgroundColor = Color.FromArgb(255, 248, 47, 209),
            };

            _shellTileSchedule = new ShellTileSchedule(ShellTile.ActiveTiles.FirstOrDefault(), tileData)
            {
                Interval = UpdateInterval.EveryHour,
                Recurrence = UpdateRecurrence.Interval
            };




            // Attempt to find the Push Channel.
            // Contains the created/found push channel instance.

            var pushChannel = HttpNotificationChannel.Find("MyPushChannel");

            // If no channel was found, then create a new connection to the push service.
            if (pushChannel == null)
            {
#if DEBUG
                // For purposes of this demo, let's just print it out
                Dispatcher.BeginInvoke(() => MessageBox.Show("Channel was null"));
#endif

                // Get a new channel and name it
                pushChannel = new HttpNotificationChannel("MyPushChannel");

                // Register for all the events before attempting to open the channel
                pushChannel.ChannelUriUpdated += PushChannel_ChannelUriUpdated;
                pushChannel.ErrorOccurred += PushChannel_ErrorOccurred;
                pushChannel.HttpNotificationReceived += PushChannel_HttpNotificationReceived;
                pushChannel.ShellToastNotificationReceived += PushChannel_ShellToastNotificationReceived;

                // Open the channel
                pushChannel.Open();


                // TODO: ADD THE URI OF YOUR SERVER WHERE TILE IMAGES ARE RETRIEVED FROM
                var uris = new Collection<Uri>
                {
                    new Uri("http://pushtestserver.cloudapp.net")
                };


                // Bind this new channel for tile and toast events.
                pushChannel.BindToShellTile(uris);
                pushChannel.BindToShellToast();
            }
            else
            {
                // Channel was found, just register for all the events.
                pushChannel.ChannelUriUpdated += PushChannel_ChannelUriUpdated;
                pushChannel.ErrorOccurred += PushChannel_ErrorOccurred;
                pushChannel.HttpNotificationReceived += PushChannel_HttpNotificationReceived;
                pushChannel.ShellToastNotificationReceived += PushChannel_ShellToastNotificationReceived;

                // Capture the URI for the channel so we can print it
                _pushChannelUri = pushChannel.ChannelUri;

                // Print the uri
                Debug.WriteLine(_pushChannelUri == null ? "PushChannel.ChannelUri is null" : _pushChannelUri.ToString());
            }
        }

        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            // See if we're already the lock provider on launch
            LockHelper(new Uri("", UriKind.RelativeOrAbsolute), "check");
        }

        // Event handler for when the Push URI has been updated.
        private void PushChannel_ChannelUriUpdated(object sender, NotificationChannelUriEventArgs e)
        {
            // Here you would normally send the URI up to your service.
            // I'd recommend you send some kind of unique ID for the device as well to tie them together
            _pushChannelUri = e.ChannelUri;
            Debug.WriteLine(_pushChannelUri.ToString());

#if DEBUG
            // For purposes of this demo, let's just print it out
            Dispatcher.BeginInvoke(() => MessageBox.Show(String.Format("Channel Uri is {0}", _pushChannelUri)));
#endif
        }

        // Error event handler for push notifications.
        private void PushChannel_ErrorOccurred(object sender, NotificationChannelErrorEventArgs e)
        {
            Dispatcher.BeginInvoke(() =>
            {
                var errorString = String.Format("A push notification {0} error occurred.  {1} ({2}) {3}",
                    e.ErrorType, e.Message, e.ErrorCode, e.ErrorAdditionalData);

                MessageBox.Show(errorString);
            });
        }

        // Event handler for when a raw notification has been recieved.
        private void PushChannel_HttpNotificationReceived(object sender, HttpNotificationEventArgs e)
        {
            try
            {
                string message;
                using (var reader = new StreamReader(e.Notification.Body))
                {
                    message = reader.ReadToEnd();
                }

                Dispatcher.BeginInvoke(() =>
                {
                    MessageBox.Show(String.Format("Raw received {0}:\n{1}", DateTime.Now.ToShortTimeString(), message));
                });
            }
            catch
            {
                // Unable to parse the received notification
                Debugger.Break();
            }
        }

        // Event handler for when a toast has been recieved.
        private void PushChannel_ShellToastNotificationReceived(object sender, NotificationEventArgs e)
        {
            var message = new StringBuilder();

            message.AppendFormat("Received Toast {0}:\n", DateTime.Now.ToShortTimeString());

            // Parse out the information that was part of the message.
            foreach (string key in e.Collection.Keys)
            {
                message.AppendFormat("{0}: {1}\n", key, e.Collection[key]);

                if (string.Compare(key, "wp:Param", CultureInfo.InvariantCulture, CompareOptions.IgnoreCase) == 0)
                {
                }
            }

            // Display a dialog of all the fields in the toast.
            Dispatcher.BeginInvoke(() => MessageBox.Show(message.ToString()));
        }

        /// <summary>
        ///     Demonstrates how to handle special parameters that may be passed as part of navigation
        ///     "WallpaperSettings" in the key passed in when your app is launched from the "Open App" button
        ///     in the Lock Settings for the phone.  You're for a value of 1 here.
        ///     tileValueExists is just showing how you could grab the launch parameters from a secondary
        ///     tile and take an action if appropriate.
        /// </summary>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.NavigationMode != NavigationMode.Back)
            {
                const string lockscreenKey = "WallpaperSettings";
                string lockscreenValue;

                bool lockscreenValueExists = NavigationContext.QueryString.TryGetValue(lockscreenKey, out lockscreenValue);

                if (lockscreenValueExists)
                {
#if DEBUG
                    Dispatcher.BeginInvoke(() =>
                    {
                        string text = String.Format("I found this key: {0}" + Environment.NewLine
                            + "With this value: {1}", lockscreenKey, lockscreenValue);
                        MessageBox.Show(text);
                    });
#endif
                    MainPagePivot.SelectedIndex = 1;
                }

                const string agentUpdatedLockscreenKey = "agentLockscreen";
                string agentUpdatedLockscreenValue;

                bool agentUpdatedLockscreenValueExists = NavigationContext.QueryString.TryGetValue(agentUpdatedLockscreenKey, out agentUpdatedLockscreenValue);

                if (agentUpdatedLockscreenValueExists)
                {
#if DEBUG
                    Dispatcher.BeginInvoke(() =>
                        MessageBox.Show(String.Format("I found this key: {0}" + Environment.NewLine + "With this value: {1}", agentUpdatedLockscreenKey, agentUpdatedLockscreenValue)));
#endif
                    MainPagePivot.SelectedIndex = 1;
                }

                const string tileKey = "how";
                string tileValue;

                bool tileValueExists = NavigationContext.QueryString.TryGetValue(tileKey, out tileValue);

                if (tileValueExists)
                {
#if DEBUG
                    Dispatcher.BeginInvoke(() => MessageBox.Show(String.Format("You started me from secondary tile: {0}", tileValue)));
#endif
                }

                const string toastKey = "toast";
                string toastValue;

                bool toastValueExists = NavigationContext.QueryString.TryGetValue(toastKey, out toastValue);

                if (toastValueExists)
                {
#if DEBUG
                    Dispatcher.BeginInvoke(
                        () =>
                            MessageBox.Show(
                                String.Format("I found this key: {0}" + Environment.NewLine + "With this value: {1}",
                                    toastKey, toastValue)));
#endif
                }
            }
        }

        private void StartPeriodicAgent()
        {
            // Variable for tracking enabled status of background agents for this app.
            _agentsAreEnabled = true;

            // Obtain a reference to the period task, if one exists
            _periodicTask = ScheduledActionService.Find(PeriodicTaskName) as PeriodicTask;

            // If the task already exists and background agents are enabled for the
            // application, you must remove the task and then add it again to update 
            // the schedule
            if (_periodicTask != null)
            {
                RemoveAgent(PeriodicTaskName);
            }

            _periodicTask = new PeriodicTask(PeriodicTaskName);

            // The description is required for periodic agents. This is the string that the user
            // will see in the background services Settings page on the device.
            _periodicTask.Description = "Periodic task for updating Live Lock";

            // Place the call to Add in a try block in case the user has disabled agents.
            try
            {
                ScheduledActionService.Add(_periodicTask);
                ScheduledActionService.LaunchForTest(PeriodicTaskName, TimeSpan.FromSeconds(5));
            }
            catch (InvalidOperationException exception)
            {
                if (exception.Message.Contains("BNS Error: The action is disabled"))
                {
                    MessageBox.Show("Background agents for this application have been disabled by the user.");
                    _agentsAreEnabled = false;
                }

                if (
                    exception.Message.Contains(
                        "BNS Error: The maximum number of ScheduledActions of this type have already been added."))
                {
                    // No user action required. The system prompts the user when the hard limit of periodic tasks has been reached.
                }
            }
            catch (SchedulerServiceException)
            {
                // No user action required.
            }
        }

        private void RemoveAgent(string name)
        {
            try
            {
                ScheduledActionService.Remove(name);
            }
            catch (Exception)
            {
            }
        }

        // Button for asking to be the lock screen background provider
        private void btnAllow_Click(object sender, RoutedEventArgs e)
        {
            LockHelper(new Uri("", UriKind.RelativeOrAbsolute), "allow");
        }

        // Button for actually setting a lock screen background image from within the app package
        private void btnSet_Click(object sender, RoutedEventArgs e)
        {
            var myImageUri = new Uri("ms-appx:///Assets/Lock/Apollo8.jpg", UriKind.Absolute);
            Debug.WriteLine(myImageUri.ToString());

            LockHelper(myImageUri, "set");
            SetButtonStates(true, true, false, false, false);
        }

        // Button for picking an image from the photo library to be the lock screen background image
        private void btnPick_Click(object sender, RoutedEventArgs e)
        {
            GetNewImage();
        }

        // Button for resetting the lock screen background image to the default image from the app package
        private void btnReset_Click(object sender, RoutedEventArgs e)
        {
            var myImageUri = new Uri("ms-appx:///DefaultLockScreen.jpg", UriKind.Absolute);
            Debug.WriteLine(myImageUri.ToString());

            LockHelper(myImageUri, "reset");
            SetButtonStates(true, false, false, true, false);
        }



        // Button for updating the lock screen background image using an agent
        private void btnLiveLock_Click(object sender, RoutedEventArgs e)
        {
            LockHelper(new Uri("", UriKind.RelativeOrAbsolute), "allow");
            Thread.Sleep(500);
            StartPeriodicAgent();
            SetButtonStates(true, false, false, false, true);
        }

        // Photo chooser task code
        private void GetNewImage()
        {
            _photoChooserTask = new PhotoChooserTask();
            _photoChooserTask.PixelWidth = Convert.ToInt32(Application.Current.Host.Content.ActualWidth);
            _photoChooserTask.PixelHeight = Convert.ToInt32(Application.Current.Host.Content.ActualHeight);
            _photoChooserTask.Completed += SelectphotoCompleted;
            _photoChooserTask.Show();
        }

        // Write the image from the photo chooser task to isoloated storage and then set the lock screen backround
        private void SelectphotoCompleted(object sender, PhotoResult e)
        {
            if (e.TaskResult == TaskResult.OK)
            {
                var bitmap = new BitmapImage();
                bitmap.SetSource(e.ChosenPhoto);

                using (IsolatedStorageFile myIsolatedStorage = IsolatedStorageFile.GetUserStoreForApplication())
                {
                    string jpgFileName = String.Format("{0}_newImage.jpg", DateTime.Now.Ticks);

                    if (!myIsolatedStorage.FileExists(jpgFileName))
                    {
                        IsolatedStorageFileStream fileStream = myIsolatedStorage.CreateFile(jpgFileName);

                        var wb = new WriteableBitmap(bitmap);

                        wb.SaveJpeg(fileStream, wb.PixelWidth, wb.PixelHeight, 0, 85);
                        fileStream.Close();

                        var myImageUri = new Uri("ms-appdata:///local/" + jpgFileName, UriKind.Absolute);
                        LockHelper(myImageUri, "pick");
                        SetButtonStates(true, false, true, false, false);
                    }
                }
            }
            else
            {
                btnPick.IsChecked = false;
            }
        }

        // Helper for doing all the different lock actions possible
        private async void LockHelper(Uri backgroundImageUri, string backgroundAction)
        {
            try
            {
                switch (backgroundAction)
                {
                    case "allow":
                        {
                            await LockScreenManager.RequestAccessAsync();
                            _isLockScreenProvider = LockScreenManager.IsProvidedByCurrentApplication;
                            if (_isLockScreenProvider) btnAllow.IsChecked = true;
                            else MessageBox.Show("You said no, so I can't update your lock screen.");
                        }
                        break;
                    case "set":
                    case "pick":
                    case "reset":
                        {
                            await LockScreenManager.RequestAccessAsync();
                            _isLockScreenProvider = LockScreenManager.IsProvidedByCurrentApplication;
                            if (_isLockScreenProvider)
                            {
                                LockScreen.SetImageUri(backgroundImageUri);
                                Debug.WriteLine("New current image set to {0}", backgroundImageUri);
                            }
                            else
                            {
                                MessageBox.Show("You said no, so I can't update your lock screen.");
                            }

                            // Obtain a reference to the period task, if one exists
                            _periodicTask = ScheduledActionService.Find(PeriodicTaskName) as PeriodicTask;

                            // If the task already exists and background agents are enabled for the
                            // application, you must remove the task and then add it again to update 
                            // the schedule
                            if (_periodicTask != null)
                            {
                                RemoveAgent(PeriodicTaskName);
                            }

                            // Variable for tracking enabled status of background agents for this app.
                            _agentsAreEnabled = false;
                        }
                        break;
                    case "check":
                        {
                            _isLockScreenProvider = LockScreenManager.IsProvidedByCurrentApplication;
                            if (_isLockScreenProvider) btnAllow.IsChecked = true;
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
        }

        // Button state handling
        private void SetButtonStates(bool state1, bool state2, bool state3, bool state4, bool state6)
        {
            btnAllow.IsChecked = state1;
            btnSet.IsChecked = state2;
            btnPick.IsChecked = state3;
            btnReset.IsChecked = state4;
            btnLiveLock.IsChecked = state6;
        }

        // Button toggle handling
        private void btnPrimaryCSharp_Click(object sender, RoutedEventArgs e)
        {
            btnPrimaryXaml.IsChecked = false;
            btnPrimaryCSharp.IsChecked = true;
        }

        // Button toggle handling
        private void btnPrimaryXaml_Click(object sender, RoutedEventArgs e)
        {
            btnPrimaryCSharp.IsChecked = false;
            btnPrimaryXaml.IsChecked = true;
        }

        // Button toggle handling
        private void btnPrimaryLocal_Click(object sender, RoutedEventArgs e)
        {
            btnPrimaryRemote.IsChecked = false;
            btnPrimaryLocal.IsChecked = true;
        }

        // Button toggle handling
        private void btnPrimaryRemote_Click(object sender, RoutedEventArgs e)
        {
            btnPrimaryLocal.IsChecked = false;
            btnPrimaryRemote.IsChecked = true;
        }

        // Button for updating the primary (iconic) tile
        private void btnPrimaryUpdate_Click(object sender, RoutedEventArgs e)
        {
            _tileHelper.IconicTile(
                tileId: new Uri("/", UriKind.Relative),
                title: "10...9...8...7...6...5...4...3...2...1...",
                widecontent1: "We came to explore the moon",
                widecontent2: "we discovered the earth...",
                widecontent3: "astronaut - Bill Sanders",
                count: 8,
                smalliconimage: GetSmallIconImage(false, btnPrimaryLocal.IsChecked.GetValueOrDefault()),
                iconimage: GetIconImage(false, btnPrimaryLocal.IsChecked.GetValueOrDefault()),
                backgroundcolor: new Color { A = 255, R = 0, G = 148, B = 255 },
                tileAction: "update",
                tileProperties: btnPrimaryCSharp.IsChecked.GetValueOrDefault()
                );
        }

        // Button toggle handling
        private void btnPrimaryReset_Click(object sender, RoutedEventArgs e)
        {
            _tileHelper.IconicTile(
                tileId: new Uri("/", UriKind.Relative),
                title: "The Final Countdown",
                widecontent1: "",
                widecontent2: "",
                widecontent3: "",
                count: 0,
                smalliconimage: new Uri("/Assets/Tiles/iconic_smalliconimage.png", UriKind.Relative),
                iconimage: new Uri("/Assets/Tiles/iconic_iconimage.png", UriKind.Relative),
                backgroundcolor: new Color { A = 255, R = 255, G = 0, B = 0 },
                tileAction: "update",
                tileProperties: btnPrimaryCSharp.IsChecked.GetValueOrDefault());
        }

        // Button toggle handling
        private void btnIconicCSharp_Click(object sender, RoutedEventArgs e)
        {
            btnIconicXaml.IsChecked = false;
            btnIconicCSharp.IsChecked = true;
        }

        // Button toggle handling
        private void btnIconicXaml_Click(object sender, RoutedEventArgs e)
        {
            btnIconicCSharp.IsChecked = false;
            btnIconicXaml.IsChecked = true;
        }

        // Button toggle handling
        private void btnIconicLocal_Click(object sender, RoutedEventArgs e)
        {
            btnIconicRemote.IsChecked = false;
            btnIconicLocal.IsChecked = true;
        }

        // Button toggle handling
        private void btnIconicRemote_Click(object sender, RoutedEventArgs e)
        {
            btnIconicLocal.IsChecked = false;
            btnIconicRemote.IsChecked = true;
        }

        // Button for creating an iconic tile
        private void btnIconicCreate_Click(object sender, RoutedEventArgs e)
        {
            _tileHelper.IconicTile(
                tileId: new Uri("/MainPage.xaml?how=iconic", UriKind.Relative),
                title: "The Final Countdown",
                widecontent1: "",
                widecontent2: "",
                widecontent3: "",
                count: 0,
                smalliconimage: GetSmallIconImage(true, btnIconicLocal.IsChecked.GetValueOrDefault()),
                iconimage: GetIconImage(true, btnIconicLocal.IsChecked.GetValueOrDefault()),
                backgroundcolor: new Color { A = 0, R = 0, G = 0, B = 0 },
                tileAction: "create",
                tileProperties: btnIconicCSharp.IsChecked.GetValueOrDefault());
        }

        // Button for updating an iconic tile
        private void btnIconicUpdate_Click(object sender, RoutedEventArgs e)
        {
            _tileHelper.IconicTile(
                tileId: new Uri("/MainPage.xaml?how=iconic", UriKind.Relative),
                title: "10...9...8...7...6...5...4...3...2...1...",
                widecontent1: "We came to explore the moon",
                widecontent2: "we discovered the earth...",
                widecontent3: "astronaut - Bill Sanders",
                count: 8,
                smalliconimage: GetSmallIconImage(false, btnIconicLocal.IsChecked.GetValueOrDefault()),
                iconimage: GetIconImage(false, btnIconicLocal.IsChecked.GetValueOrDefault()),
                backgroundcolor: new Color { A = 255, R = 0, G = 148, B = 255 },
                tileAction: "update",
                tileProperties: btnPrimaryCSharp.IsChecked.GetValueOrDefault());
        }

        // Button for deleting an iconic tile
        private void btnIconicDelete_Click(object sender, RoutedEventArgs e)
        {
            _tileHelper.IconicTile(
                tileId: new Uri("/MainPage.xaml?how=iconic", UriKind.Relative),
                tileAction: "delete");
        }

        // Return the SmallIconImage based on if the update is local/remote or create/update
        public Uri GetSmallIconImage(bool isCreate, bool isLocal)
        {
            Uri smallIconImage;
            if (isLocal)
            {
                smallIconImage = isCreate
                    ? new Uri("/Assets/Tiles/iconic_smalliconimage.png", UriKind.Relative)
                    : new Uri("/Assets/Tiles/update_iconic_smalliconimage.png", UriKind.Relative);
            }
            else
            {
                // TODO: ADD THE REMOTE URIS FOR YOUR IMAGES
                smallIconImage = isCreate
                    ? new Uri("http://pushtestserver.blob.core.windows.net/tiles/iconic_smalliconimage.png", UriKind.Absolute)
                    : new Uri("http://pushtestserver.blob.core.windows.net/tiles/update_iconic_smalliconimage.png", UriKind.Absolute);
            }

            return smallIconImage;
        }

        // Return the IconImage based on if the update is local/remote or create/update
        public Uri GetIconImage(bool isCreate, bool isLocal)
        {
            Uri iconImage;
            if (isLocal)
            {
                iconImage = isCreate
                    ? new Uri("/Assets/Tiles/iconic_iconimage.png", UriKind.Relative)
                    : new Uri("/Assets/Tiles/update_iconic_iconimage.png", UriKind.Relative);
            }
            else
            {
                // TODO: ADD THE REMOTE URIS FOR YOUR IMAGES
                iconImage = isCreate
                    ? new Uri("http://pushtestserver.blob.core.windows.net/tiles/iconic_iconimage.png", UriKind.Absolute)
                    : new Uri("http://pushtestserver.blob.core.windows.net/tiles/update_iconic_iconimage.png", UriKind.Absolute);
            }

            return iconImage;
        }

        // Button toggle handling
        private void btnCycleCSharp_Click(object sender, RoutedEventArgs e)
        {
            btnCycleXaml.IsChecked = false;
            btnCycleCSharp.IsChecked = true;
        }

        // Button toggle handling
        private void btnCycleXaml_Click(object sender, RoutedEventArgs e)
        {
            btnCycleCSharp.IsChecked = false;
            btnCycleXaml.IsChecked = true;
        }

        // Button for creating a cycle tile
        private void btnCycleCreate_Click(object sender, RoutedEventArgs e)
        {
            _tileHelper.CycleTile(
                tileId: new Uri("/MainPage.xaml?how=cycle", UriKind.Relative),
                title: "TFC - Photo Stream",
                count: 0,
                smallbackgroundimage: new Uri("/Assets/Tiles/cycle_smallbackgroundimage.png", UriKind.Relative),
                cycleimage1: new Uri("/Assets/Tiles/cycle_cycleimage1.jpg", UriKind.Relative),
                cycleimage2: new Uri("/Assets/Tiles/cycle_cycleimage2.jpg", UriKind.Relative),
                cycleimage3: new Uri("/Assets/Tiles/cycle_cycleimage3.jpg", UriKind.Relative),
                cycleimage4: new Uri("/Assets/Tiles/cycle_cycleimage4.jpg", UriKind.Relative),
                cycleimage5: new Uri("/Assets/Tiles/cycle_cycleimage5.jpg", UriKind.Relative),
                cycleimage6: new Uri("/Assets/Tiles/cycle_cycleimage6.jpg", UriKind.Relative),
                cycleimage7: new Uri("/Assets/Tiles/cycle_cycleimage7.jpg", UriKind.Relative),
                cycleimage8: new Uri("/Assets/Tiles/cycle_cycleimage8.jpg", UriKind.Relative),
                cycleimage9: new Uri("/Assets/Tiles/cycle_cycleimage9.jpg", UriKind.Relative),
                tileAction: "create",
                tileProperties: btnCycleCSharp.IsChecked.GetValueOrDefault());
        }

        // Button for updating a cycle tile
        private void btnCycleUpdate_Click(object sender, RoutedEventArgs e)
        {
            _tileHelper.CycleTile(
                tileId: new Uri("/MainPage.xaml?how=cycle", UriKind.Relative),
                title: "TFC - Photo Stream - New!",
                count: 8,
                smallbackgroundimage: new Uri("/Assets/Tiles/update_cycle_smallbackgroundimage.png", UriKind.Relative),
                cycleimage1: new Uri("/Assets/Tiles/update_cycle_cycleimage1.jpg", UriKind.Relative),
                cycleimage2: new Uri("/Assets/Tiles/update_cycle_cycleimage2.jpg", UriKind.Relative),
                cycleimage3: new Uri("/Assets/Tiles/update_cycle_cycleimage3.jpg", UriKind.Relative),
                cycleimage4: new Uri("/Assets/Tiles/update_cycle_cycleimage4.jpg", UriKind.Relative),
                cycleimage5: new Uri("/Assets/Tiles/update_cycle_cycleimage5.jpg", UriKind.Relative),
                cycleimage6: new Uri("/Assets/Tiles/update_cycle_cycleimage6.jpg", UriKind.Relative),
                cycleimage7: new Uri("/Assets/Tiles/update_cycle_cycleimage7.jpg", UriKind.Relative),
                cycleimage8: new Uri("/Assets/Tiles/update_cycle_cycleimage8.jpg", UriKind.Relative),
                cycleimage9: new Uri("/Assets/Tiles/update_cycle_cycleimage9.jpg", UriKind.Relative),
                tileAction: "update",
                tileProperties: btnCycleCSharp.IsChecked.GetValueOrDefault());
        }

        // Button for deleting a cycle tile
        private void btnCycleDelete_Click(object sender, RoutedEventArgs e)
        {
            _tileHelper.CycleTile(
                tileId: new Uri("/MainPage.xaml?how=cycle", UriKind.Relative),
                tileAction: "delete");
        }

        // Button toggle handling
        private void btnFlipCSharp_Click(object sender, RoutedEventArgs e)
        {
            btnFlipXaml.IsChecked = false;
            btnFlipCSharp.IsChecked = true;
        }

        // Button toggle handling
        private void btnFlipXaml_Click(object sender, RoutedEventArgs e)
        {
            btnFlipCSharp.IsChecked = false;
            btnFlipXaml.IsChecked = true;
        }

        // Button toggle handling
        private void btnFlipLocal_Click(object sender, RoutedEventArgs e)
        {
            btnFlipRemote.IsChecked = false;
            btnFlipLocal.IsChecked = true;
        }

        // Button toggle handling
        private void btnFlipRemote_Click(object sender, RoutedEventArgs e)
        {
            btnFlipLocal.IsChecked = false;
            btnFlipRemote.IsChecked = true;
        }

        // Button for creating a flip tile
        private void btnFlipCreate_Click(object sender, RoutedEventArgs e)
        {
            _tileHelper.FlipTile(
                tileId: new Uri("/MainPage.xaml?how=flip", UriKind.Relative),
                title: "TFC - Mission Facts",
                backtitle: "",
                backcontent: "Apollo 8" + Environment.NewLine + "Earthrise over the moon",
                widebackcontent: "Apollo 8" + Environment.NewLine + "First mission to break earth's orbit and orbit the moon",
                count: 0, smallbackgroundimage: GetSmallBackgroundImage(true, btnFlipLocal.IsChecked.GetValueOrDefault()),
                backgroundimage: GetBackgroundImage(true, btnFlipLocal.IsChecked.GetValueOrDefault()),
                backbackgroundimage: new Uri("", UriKind.Relative),
                widebackgroundimage: GetWideBackgroundImage(true, btnFlipLocal.IsChecked.GetValueOrDefault()),
                widebackbackgroundimage: new Uri("", UriKind.Relative),
                tileAction: "create",
                tileProperties: btnFlipCSharp.IsChecked.GetValueOrDefault());
        }

        // Button for updating a flip tile
        private void btnFlipUpdate_Click(object sender, RoutedEventArgs e)
        {
            _tileHelper.FlipTile(
                tileId: new Uri("/MainPage.xaml?how=flip", UriKind.Relative),
                title: "TFC - Mission Facts - New!",
                backtitle: "",
                backcontent: "Apollo 8" + Environment.NewLine + "Command capsule hoisting",
                widebackcontent: "Apollo 8" + Environment.NewLine + "Command capsule retrieval by U.S.S. Yorktown navy divers",
                count: 8,
                smallbackgroundimage: GetSmallBackgroundImage(false, btnFlipLocal.IsChecked.GetValueOrDefault()),
                backgroundimage: GetBackgroundImage(false, btnFlipLocal.IsChecked.GetValueOrDefault()),
                backbackgroundimage: new Uri("", UriKind.Relative),
                widebackgroundimage: GetWideBackgroundImage(false, btnFlipLocal.IsChecked.GetValueOrDefault()),
                widebackbackgroundimage: new Uri("", UriKind.Relative),
                tileAction: "update",
                tileProperties: btnFlipCSharp.IsChecked.GetValueOrDefault());
        }

        // Button for deleting a flip tile
        private void btnFlipDelete_Click(object sender, RoutedEventArgs e)
        {
            _tileHelper.FlipTile(
                tileId: new Uri("/MainPage.xaml?how=flip", UriKind.Relative),
                tileAction: "delete");
        }

        // Return the SmallBackgroundImage based on if the update is local/remote or create/update
        public Uri GetSmallBackgroundImage(bool isCreate, bool isLocal)
        {
            Uri smallBackgroundImage;
            if (isLocal)
            {
                smallBackgroundImage = isCreate
                    ? new Uri("/Assets/Tiles/flip_smallbackgroundimage.jpg", UriKind.Relative)
                    : new Uri("/Assets/Tiles/update_flip_smallbackgroundimage.jpg", UriKind.Relative);
            }
            else
            {
                // TODO: ADD THE REMOTE URIS FOR YOUR IMAGES
                smallBackgroundImage = isCreate
                    ? new Uri("http://pushtestserver.blob.core.windows.net/tiles/flip_smallbackgroundimage.jpg", UriKind.Absolute)
                    : new Uri("http://pushtestserver.blob.core.windows.net/tiles/update_flip_smallbackgroundimage.jpg", UriKind.Absolute);
            }

            return smallBackgroundImage;
        }

        // Return the BackgroundImage based on if the update is local/remote or create/update
        public Uri GetBackgroundImage(bool isCreate, bool isLocal)
        {
            Uri backgroundImage;
            if (isLocal)
            {
                backgroundImage = isCreate
                    ? new Uri("/Assets/Tiles/flip_backgroundimage.jpg", UriKind.Relative)
                    : new Uri("/Assets/Tiles/update_flip_backgroundimage.jpg", UriKind.Relative);
            }
            else
            {
                // TODO: ADD THE REMOTE URIS FOR YOUR IMAGES
                backgroundImage = isCreate
                    ? new Uri("http://pushtestserver.blob.core.windows.net/tiles/flip_backgroundimage.jpg", UriKind.Absolute)
                    : new Uri("http://pushtestserver.blob.core.windows.net/tiles/update_flip_backgroundimage.jpg", UriKind.Absolute);
            }

            return backgroundImage;
        }

        // Return the WideBackgroundImage based on if the update is local/remote or create/update
        public Uri GetWideBackgroundImage(bool isCreate, bool isLocal)
        {
            Uri wideBackgroundImage;
            if (isLocal)
            {
                wideBackgroundImage = isCreate
                    ? new Uri("/Assets/Tiles/flip_widebackgroundimage.jpg", UriKind.Relative)
                    : new Uri("/Assets/Tiles/update_flip_widebackgroundimage.jpg", UriKind.Relative);
            }
            else
            {
                // TODO: ADD THE REMOTE URIS FOR YOUR IMAGES
                wideBackgroundImage = isCreate
                    ? new Uri("http://pushtestserver.blob.core.windows.net/tiles/flip_widebackgroundimage.jpg", UriKind.Absolute)
                    : new Uri("http://pushtestserver.blob.core.windows.net/tiles/update_flip_widebackgroundimage.jpg", UriKind.Absolute);
            }

            return wideBackgroundImage;
        }

        // Helper for animating some stack panels
        private void DoAnimation(object sender, StackPanel stackPanel)
        {
            var button = (RoundToggleButton)sender;

            if (stackPanel.Height == 0)
            {
                Grow.Stop();
                //StackPanel myStack = (StackPanel)sender;
                growDoubleAnimation.SetValue(Storyboard.TargetNameProperty, stackPanel.Name);
                growDoubleAnimation.To = 55;
                Grow.Begin();
                button.IsChecked = true;
               
            }
            else
            {
                Shrink.Stop();
                shrinkDoubleAnimation.SetValue(Storyboard.TargetNameProperty, stackPanel.Name);
                Shrink.Begin();
                button.IsChecked = false;
            }
        }

        // Helper for toggling buttons
        private void DoToggle(object sender, RoutedEventArgs e)
        {
            var button = (RoundToggleButton)sender;

            if (button.Name == "btnPrimaryToggle") DoAnimation(sender as RoundToggleButton, primaryStack);
            else if (button.Name == "btnIconicToggle") DoAnimation(sender as RoundToggleButton, iconicStack);
            else if (button.Name == "btnFlipToggle") DoAnimation(sender as RoundToggleButton, flipStack);
            else if (button.Name == "btnCycleToggle") DoAnimation(sender as RoundToggleButton, cycleStack);
        }


        /// <summary>
        ///     This is how we navigate the user to the phones Lock Settings so they can choose things like
        ///     Quick Status, Detailed Status and even lock screen background (although you can ask for that one
        ///     programatically as well).
        ///     Having this somewhere in your settings for your app is really important so the user can find
        ///     how to set your app on lock easily.
        /// </summary>
        private async void btnPrimarySet_Click(object sender, RoutedEventArgs e)
        {
            // Launch URI for settings page.
            await Launcher.LaunchUriAsync(new Uri("ms-settings-lock:"));
        }

        private void btnScheduleStart_Click(object sender, RoutedEventArgs e)
        {
            _shellTileSchedule.Start();
        }

        private void btnScheduleStop_Click(object sender, RoutedEventArgs e)
        {
            if (_shellTileSchedule != null)
            {
                _shellTileSchedule.Stop();

                // TODO: OPTIONALLY RESET THE TILE TO IT'S DEFAULT STATE IN YOUR APP SCENARIO
            }
        }

        private void ScheduleIntervalChanged(object sender, RoutedEventArgs e)
        {
            var radio = (RadioButton) sender;

            if (_shellTileSchedule != null)
            {
                // Parse the Tag from the RadioButton into the correct Enum
                _shellTileSchedule.Interval = (UpdateInterval) Enum.Parse(typeof (UpdateInterval), radio.Tag.ToString());
            }
        }

        private void ScheduleRecurrenceChanged(object sender, RoutedEventArgs e)
        {
            var radio = (RadioButton)sender;

            if (_shellTileSchedule != null)
            {
                // Parse the Tag from the RadioButton into the correct Enum
                _shellTileSchedule.Recurrence = (UpdateRecurrence) Enum.Parse(typeof (UpdateRecurrence), radio.Tag.ToString());
            }
        }
    }
}