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
using System.IO.IsolatedStorage;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Phone.Scheduler;
using Microsoft.Phone.Shell;
using Windows.Phone.System.UserProfile;

namespace ScheduledTaskAgent1
{
    public class ScheduledAgent : ScheduledTaskAgent
    {
        private static volatile bool _classInitialized;

        /// <remarks>
        /// ScheduledAgent constructor, initializes the UnhandledException handler
        /// </remarks>
        public ScheduledAgent()
        {
            if (!_classInitialized)
            {
                _classInitialized = true;
                // Subscribe to the managed exception handler
                Deployment.Current.Dispatcher.BeginInvoke(delegate
                {
                    Application.Current.UnhandledException += ScheduledAgent_UnhandledException;
                });
            }
        }

        /// Code to execute on Unhandled Exceptions
        private void ScheduledAgent_UnhandledException(object sender, ApplicationUnhandledExceptionEventArgs e)
        {
            if (System.Diagnostics.Debugger.IsAttached)
            {
                // An unhandled exception has occurred; break into the debugger
                System.Diagnostics.Debugger.Break();
            }
        }


        /// <summary>
        /// Agent that runs a scheduled task
        /// </summary>
        /// <param name="task">
        /// The invoked task
        /// </param>
        /// <remarks>
        /// This method is called when a periodic or resource intensive task is invoked
        /// </remarks>
        protected override void OnInvoke(ScheduledTask task)
        {
            Deployment.Current.Dispatcher.BeginInvoke(() =>
            {

                //I'm faking a weather image update and some text/image composition
                var lockBackgroundImage = new Image
                {
                    Source = new BitmapImage(new Uri("/Assets/Lock/Background.jpg", UriKind.RelativeOrAbsolute)),
                    Width = 480,
                    Height = 800
                };

                var lockWeatherImage = new Image
                {
                    Source = new BitmapImage(new Uri("/Assets/Lock/CloudSun.png", UriKind.RelativeOrAbsolute)),
                    Width = 88,
                    Height = 88
                };

                var lockTextBlock = new TextBlock
                {
                    Text = "san francisco" + Environment.NewLine + "72 degrees" + Environment.NewLine +
                           "partially sunny",
                    FontSize = 24,
                    Foreground = new SolidColorBrush(Colors.White),
                    FontFamily = new FontFamily("Segoe WP SemiLight")
                };

                // The LockScreen.SetImageUri requires that the Uri of the new image is different than the current one.
                // Determine the name to use, doing an A-B toggle to have always a maximum 
                // of 2 images (current and previous), and no need to implement a cache purging mechanism.
                string fileName;
                Uri currentImage;

                try
                {
                    currentImage = LockScreen.GetImageUri();
                }
                catch (Exception)
                {
                    currentImage = new Uri("ms-appdata:///local/LiveLockBackground_A.jpg", UriKind.Absolute);
                }

                if (currentImage.ToString().EndsWith("_A.jpg"))
                {
                    fileName = "LiveLockBackground_B.jpg";
                }
                else
                {
                    fileName = "LiveLockBackground_A.jpg";
                }

                var lockImage = string.Format("{0}", fileName);
                var isoStoreLockImage = new Uri(string.Format("ms-appdata:///local/{0}", fileName), UriKind.Absolute);

                using (IsolatedStorageFile store = IsolatedStorageFile.GetUserStoreForApplication())
                {
                    var stream = store.CreateFile(lockImage);

                    var bitmap = new WriteableBitmap(480, 800);

                    bitmap.Render(lockBackgroundImage, new TranslateTransform());

                    bitmap.Render(lockWeatherImage, new TranslateTransform()
                    {
                        X = 12,
                        Y = 36
                    });

                    bitmap.Render(lockTextBlock, new TranslateTransform()
                    {
                        X = 24,
                        Y = 118
                    });

                    bitmap.Invalidate();
                    bitmap.SaveJpeg(stream, 480, 800, 0, 100);

                    stream.Close();

                }

                bool isProvider = LockScreenManager.IsProvidedByCurrentApplication;
                if (isProvider)
                {
                    LockScreen.SetImageUri(isoStoreLockImage);
                    System.Diagnostics.Debug.WriteLine("New current image set to {0}", isoStoreLockImage);
                }

                var toast = new ShellToast
                {
                    Title = "The Final Countdown",
                    Content = "The lock screen was updated...",
                    NavigationUri = new Uri("/MainPage.xaml?agentLockscreen=1", UriKind.RelativeOrAbsolute)
                };

                toast.Show();
            });

            NotifyComplete();
        }
    }
}