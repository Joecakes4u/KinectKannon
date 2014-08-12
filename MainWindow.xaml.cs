﻿//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace KinectKannon
{
    using Microsoft.Kinect;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using System.Timers;
    using System.Windows.Input;
    using KinectKannon.Rendering;
    /// <summary>
    /// Interaction logic for MainWindow
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor kinectSensor = null;

        /// <summary>
        /// Coordinate mapper to map one type of point to another
        /// </summary>
        private CoordinateMapper coordinateMapper = null;

        /// <summary>
        /// Reader for body frames
        /// </summary>
        private BodyFrameReader bodyFrameReader = null;

        /// <summary>
        /// Reader for color frames
        /// </summary>
        private ColorFrameReader colorFrameReader = null;

        /// <summary>
        /// Array for the bodies
        /// </summary>
        private Body[] bodies = null;

        /// <summary>
        /// Describes an arbitrary number which represents how far left or right the cannon is position
        /// This range of this value is TBD
        /// </summary>
        private double cannonXPosition = 0.0f;

        /// <summary>
        /// Describes an arbitrary number which represents how high up or low the cannon is position
        /// This range of this value is TBD
        /// </summary>
        private double cannonYPosition = 0.0f;

        /// <summary>
        /// The current tracking mode of the system
        /// </summary>
        private TrackingMode trackingMode = TrackingMode.MANUAL;

        /// <summary>
        /// The frame rate that will be displayed.
        /// </summary>
        private double debugFrameRate = 0.0f;

        /// <summary>
        /// The number which olds the amount of frames since the last invocation of the timer.
        /// </summary>
        private int elapsedFrames = 0;
        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        /// 
        private ColorFrameRenderer colorRenderer;

        /// <summary>
        /// Responsible for drawing the HUD layer
        /// </summary>
        private HudRenderer hudRenderer;

        private string statusText = null;

        public MainWindow()
        {
            // one sensor is currently supported
            this.kinectSensor = KinectSensor.GetDefault();

            // get the coordinate mapper
            this.coordinateMapper = this.kinectSensor.CoordinateMapper;

            // open the reader for the body frames
            this.bodyFrameReader = this.kinectSensor.BodyFrameSource.OpenReader();

            this.colorFrameReader = this.kinectSensor.ColorFrameSource.OpenReader();

            // get the depth (display) extents
            FrameDescription jointFrameDescription = this.kinectSensor.DepthFrameSource.FrameDescription;
            
            FrameDescription colorFrameDescription = this.kinectSensor.ColorFrameSource.CreateFrameDescription(ColorImageFormat.Bgra);

            colorRenderer = new ColorFrameRenderer(colorFrameDescription.Width, colorFrameDescription.Height, jointFrameDescription.Width, jointFrameDescription.Height);
            var drawingGroup = new DrawingGroup();
            var drawingImage = new DrawingImage(drawingGroup);
            hudRenderer = new HudRenderer(drawingGroup, drawingImage, colorFrameDescription.Width, colorFrameDescription.Height);

            this.colorFrameReader.FrameArrived += this.Reader_ColorFrameArrived;

            // set IsAvailableChanged event notifier
            this.kinectSensor.IsAvailableChanged += this.Sensor_IsAvailableChanged;

            // open the sensor
            this.kinectSensor.Open();

            // set the status text TODO: change namespace name in resources
            this.StatusText = this.kinectSensor.IsAvailable ? Microsoft.Samples.Kinect.BodyBasics.Properties.Resources.RunningStatusText
                                                            : Microsoft.Samples.Kinect.BodyBasics.Properties.Resources.NoSensorStatusText;
            
            // use the window object as the view model in this simple example
            this.DataContext = this;

            // initialize the components (controls) of the window
            this.InitializeComponent();

            //register the code which will tell the system what to do when keys are pressed
            SetupKeyHandlers();

            //draw the headsup display initially
            this.hudRenderer.RenderHud(new HudRenderingParameters()
            {
                CannonX = this.CannonX,
                CannonY = this.CannonY,
                StatusText = this.statusText,
                SystemReady = (this.kinectSensor.IsAvailable && this.kinectSensor.IsOpen),
                FrameRate = this.FrameRate,
                TrackingMode = this.trackingMode
            });

            //debug start frame rate counter
            FPSTimerStart();
        }

        private void SetupKeyHandlers()
        {
            KeyDown += MainWindow_KeyDown;
        }

        /// <summary>
        /// Handles an 'controller' actions using the keyboard interface
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void MainWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            //TODO: This is where the logic for controlling the servos will be placed
            if(e.Key == System.Windows.Input.Key.Up && this.trackingMode == TrackingMode.MANUAL)
            {
                this.cannonYPosition += .1;
            }
            else if (e.Key == System.Windows.Input.Key.Down && this.trackingMode == TrackingMode.MANUAL)
            {
                this.cannonYPosition -= .1;
            }
            else if (e.Key == System.Windows.Input.Key.Left && this.trackingMode == TrackingMode.MANUAL)
            {
                this.cannonXPosition -= .1;
            }
            else if (e.Key == System.Windows.Input.Key.Right && this.trackingMode == TrackingMode.MANUAL)
            {
                this.cannonXPosition += .1;
            }
            else if (e.Key == Key.NumPad1 || e.Key == Key.D1)
            {
                this.trackingMode = TrackingMode.MANUAL;
            }
            else if (e.Key == Key.NumPad2 || e.Key == Key.D2)
            {
                this.trackingMode = TrackingMode.SKELETAL;
            }
            else if (e.Key == Key.NumPad3 || e.Key == Key.D3)
            {
                this.trackingMode = TrackingMode.AUDIBLE;
            }
        }

        /// <summary>
        /// Handles the color frame data arriving from the sensor
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Reader_ColorFrameArrived(object sender, ColorFrameArrivedEventArgs e)
        {
            //render color layer
            this.colorRenderer.Reader_ColorFrameArrived(sender, e);
            elapsedFrames++;
            //draw the headsup display initially
            this.hudRenderer.RenderHud(new HudRenderingParameters()
            {
                CannonX = this.CannonX,
                CannonY = this.CannonY,
                StatusText = this.statusText,
                SystemReady = (this.kinectSensor.IsAvailable && this.kinectSensor.IsOpen),
                FrameRate = this.FrameRate,
                TrackingMode = this.trackingMode
            });
        }

        private void FPSTimerStart()
        {
            var fpsTimer = new Timer(1000);
            fpsTimer.Elapsed += fpsTimer_Elapsed;
            fpsTimer.Enabled = true;
        }

        void fpsTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            this.debugFrameRate = elapsedFrames;
            elapsedFrames = 0;
        }
        /// <summary>
        /// INotifyPropertyChangedPropertyChanged event to allow window controls to bind to changeable data
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets the bitmap to display
        /// </summary>
        public ImageSource ImageSource
        {
            get
            {
                return this.colorRenderer.ImageSource;
            }
        }

        /// <summary>
        /// Gets the bitmap to display
        /// </summary>
        public ImageSource HudSource
        {
            get
            {
                return this.hudRenderer.ImageSource;
            }
        }

        public string FrameRate
        {
            get
            {
                return String.Format("{0:0.00}", this.debugFrameRate);
            }
        }

        public string CannonX
        {
            get
            {
                return String.Format("{0:0.00}", this.cannonXPosition);
            }
        }

        public string CannonY
        {
            get
            {
                return String.Format("{0:0.00}", this.cannonYPosition);
            }
        }
        /// <summary>
        /// Gets or sets the current status text to display
        /// </summary>
        public string StatusText
        {
            get
            {
                return this.statusText;
            }

            set
            {
                if (this.statusText != value)
                {
                    this.statusText = value;

                    // notify any bound elements that the text has changed
                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("StatusText"));
                    }
                }
            }
        }

        /// <summary>
        /// Execute start up tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (this.bodyFrameReader != null)
            {
                this.bodyFrameReader.FrameArrived += this.Reader_FrameArrived;
            }
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (this.bodyFrameReader != null)
            {
                // BodyFrameReader is IDisposable
                this.bodyFrameReader.Dispose();
                this.bodyFrameReader = null;
            }

            if (this.kinectSensor != null)
            {
                this.kinectSensor.Close();
                this.kinectSensor = null;
            }
        }

        /// <summary>
        /// Handles the body frame data arriving from the sensor
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Reader_FrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            bool dataReceived = false;

            using (BodyFrame bodyFrame = e.FrameReference.AcquireFrame())
            {
                if (bodyFrame != null)
                {
                    if (this.bodies == null)
                    {
                        this.bodies = new Body[bodyFrame.BodyCount];
                    }

                    // The first time GetAndRefreshBodyData is called, Kinect will allocate each Body in the array.
                    // As long as those body objects are not disposed and not set to null in the array,
                    // those body objects will be re-used.
                    bodyFrame.GetAndRefreshBodyData(this.bodies);
                    dataReceived = true;
                }
            }

            if (dataReceived)
            {
                colorRenderer.DrawBodies(this.bodies, this.coordinateMapper);
            }
        }

        /// <summary>
        /// Handles the event which the sensor becomes unavailable (E.g. paused, closed, unplugged).
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Sensor_IsAvailableChanged(object sender, IsAvailableChangedEventArgs e)
        {
            // on failure, set the status text
            this.statusText = this.kinectSensor.IsAvailable ? Microsoft.Samples.Kinect.BodyBasics.Properties.Resources.RunningStatusText
                                                            : Microsoft.Samples.Kinect.BodyBasics.Properties.Resources.SensorNotAvailableStatusText;
        }

    }

    /// <summary>
    /// Used to determine which skeleton will be tracked if in SKELETAL tracking
    /// mode
    /// </summary>
    public enum SkeletalLetter
    {
        A = 0,
        B,
        C,
        D,
        E,
        F
    }

    /// <summary>
    /// The tracking state of the system. Used to determine if pan/tilt will be controlled
    /// autonomously or manually
    /// </summary>
    public enum TrackingMode
    {
        MANUAL,
        SKELETAL,
        AUDIBLE
    }
}