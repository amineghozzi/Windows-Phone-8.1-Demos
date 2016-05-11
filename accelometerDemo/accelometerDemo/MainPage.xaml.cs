using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Devices.Sensors;
using Windows.UI.Popups;
using Windows.UI.Core;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=391641

namespace accelometerDemo
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public Accelerometer accelerometer { get; set; }

        //Use for get efficient signal intervals between accelerometer responses
        private uint desiredReportInterval { get; set; }
        public MainPage()
        {
            this.InitializeComponent();

            
                accelerometer = Accelerometer.GetDefault();
                if (accelerometer != null)
                {
                    // Select a report interval that is both suitable for the purposes of the app and supported by the sensor.
                    // This value will be used later to activate the sensor.
                    uint minReportInterval = accelerometer.MinimumReportInterval;
                    desiredReportInterval = minReportInterval > 16 ? minReportInterval : 16;


                    accelerometer.ReportInterval = desiredReportInterval;

                    //add event for accelerometer readings
                    accelerometer.ReadingChanged += new TypedEventHandler<Accelerometer, AccelerometerReadingChangedEventArgs>(ReadingChanged);

                }
                else
                {
                    MessageDialog ms = new MessageDialog("No accelerometer Found");
                    ms.ShowAsync();
                }

            this.NavigationCacheMode = NavigationCacheMode.Required;
        }

        /// <summary>
        /// reading accelerometer changes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private async void ReadingChanged(Accelerometer sender, AccelerometerReadingChangedEventArgs args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                AccelerometerReading reading = args.Reading;
                corX.Text = String.Format("{0,5:0.00}", reading.AccelerationX);
                corY.Text = String.Format("{0,5:0.00}", reading.AccelerationY);
                corZ.Text = String.Format("{0,5:0.00}", reading.AccelerationZ);
            });
        }

        /// <summary>
        /// Invoked when this page is about to be displayed in a Frame.
        /// </summary>
        /// <param name="e">Event data that describes how this page was reached.
        /// This parameter is typically used to configure the page.</param>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            // TODO: Prepare page for display here.

            // TODO: If your application contains multiple pages, ensure that you are
            // handling the hardware Back button by registering for the
            // Windows.Phone.UI.Input.HardwareButtons.BackPressed event.
            // If you are using the NavigationHelper provided by some templates,
            // this event is handled for you.
        }
    }
}
