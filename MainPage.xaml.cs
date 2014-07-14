/*
 * Copyright © 2013 Nokia Corporation. All rights reserved.
 * Nokia and Nokia Connecting People are registered trademarks of Nokia Corporation. 
 * Other product and company names mentioned herein may be trademarks
 * or trade names of their respective owners. 
 * See LICENSE.TXT for license information.
 */

using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using RealtimeFilterDemo.Resources;
using System;
using System.Linq;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Navigation;
using Windows.Foundation;
using Windows.Phone.Media.Capture;

namespace RealtimeFilterDemo
{
    public partial class MainPage : PhoneApplicationPage
    {
        private MediaElement _mediaElement = null;
        private PhotoCaptureDevice _photoCaptureDevice = null;
        private NokiaImagingSDKEffects _cameraEffect = null;
        private CameraStreamSource _cameraStreamSource = null;
        private Semaphore _cameraSemaphore = new Semaphore(1, 1);

        public MainPage()
        {
            InitializeComponent();

            ApplicationBar = new ApplicationBar();

            var previousButton = new ApplicationBarIconButton(new Uri("/Assets/Icons/previous.png", UriKind.Relative));
            previousButton.Text = AppResources.PreviousEffectButtonText;
            previousButton.Click += PreviousButton_Click;

            ApplicationBar.Buttons.Add(previousButton);

            var nextButton = new ApplicationBarIconButton(new Uri("/Assets/Icons/next.png", UriKind.Relative));
            nextButton.Text = AppResources.NextEffectButtonText;
            nextButton.Click += NextButton_Click;

            ApplicationBar.Buttons.Add(nextButton);

            var aboutMenuItem = new ApplicationBarMenuItem();
            aboutMenuItem.Text = AppResources.AboutPageButtonText;
            aboutMenuItem.Click += AboutMenuItem_Click;

            ApplicationBar.MenuItems.Add(aboutMenuItem);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            Initialize();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            while (!_cameraSemaphore.WaitOne(100));

            Uninitialize();

            _cameraSemaphore.Release();
        }

        protected override void OnOrientationChanged(OrientationChangedEventArgs e)
        {
            base.OnOrientationChanged(e);

            AdjustOrientation();
        }

        private void AdjustOrientation()
        {
            if (_photoCaptureDevice != null)
            {
                double canvasAngle;

                if (Orientation.HasFlag(PageOrientation.LandscapeLeft))
                {
                    canvasAngle = _photoCaptureDevice.SensorRotationInDegrees - 90;
                }
                else if (Orientation.HasFlag(PageOrientation.LandscapeRight))
                {
                    canvasAngle = _photoCaptureDevice.SensorRotationInDegrees + 90;
                }
                else // PageOrientation.PortraitUp
                {
                    canvasAngle = _photoCaptureDevice.SensorRotationInDegrees;
                }



                var tmptransform = new RotateTransform() { Angle = canvasAngle };
                var previewSize = tmptransform.TransformBounds(
                    new System.Windows.Rect(
                        new System.Windows.Point(),
                        new System.Windows.Size(_photoCaptureDevice.PreviewResolution.Width, _photoCaptureDevice.PreviewResolution.Height)
                        )
                );



                double s1 = viewfinderCanvas.ActualWidth / previewSize.Width;
                double s2 = viewfinderCanvas.ActualHeight / previewSize.Height;

                //video center match Viewfinder canvas center center
                BackgroundVideoBrush.AlignmentX = AlignmentX.Center;
                BackgroundVideoBrush.AlignmentY = AlignmentY.Center;

                //Don't use a strech strategie.
                BackgroundVideoBrush.Stretch = Stretch.None;

             
                double scale = Math.Max(s1, s2); //UniformFill
               // double scale = Math.Min(s1, s2); // Uniform


                 if (_photoCaptureDevice.SensorLocation == CameraSensorLocation.Back)
                 {
                     BackgroundVideoBrush.Transform = new CompositeTransform() { Rotation = canvasAngle, CenterX = viewfinderCanvas.ActualWidth / 2, CenterY = viewfinderCanvas.ActualHeight / 2, ScaleX = scale, ScaleY = scale };
                 }
                 else
                 {
                     //Front viewfinder need to be flipped
                     BackgroundVideoBrush.Transform = new CompositeTransform() { Rotation = canvasAngle, CenterX = viewfinderCanvas.ActualWidth / 2, CenterY = viewfinderCanvas.ActualHeight / 2, ScaleX = -scale, ScaleY = scale };
                 }


                _photoCaptureDevice.SetProperty(KnownCameraGeneralProperties.EncodeWithOrientation, canvasAngle);
            }
        }

        private async void Initialize()
        {
            StatusTextBlock.Text = AppResources.MainPage_StatusTextBlock_StartingCamera;

            var camera = CameraSensorLocation.Back;
          //  var camera = CameraSensorLocation.Front;

            var resolution = PhotoCaptureDevice.GetAvailablePreviewResolutions(camera).Last();

            _photoCaptureDevice = await PhotoCaptureDevice.OpenAsync(camera, resolution);

            await _photoCaptureDevice.SetPreviewResolutionAsync(resolution);

            _cameraEffect = new NokiaImagingSDKEffects();
            _cameraEffect.PhotoCaptureDevice = _photoCaptureDevice;

            _cameraStreamSource = new CameraStreamSource(_cameraEffect, resolution);
            _cameraStreamSource.FrameRateChanged += CameraStreamSource_FPSChanged;

            _mediaElement = new MediaElement();
            _mediaElement.BufferingTime = new TimeSpan(0);
            _mediaElement.SetSource(_cameraStreamSource);

            // Using VideoBrush in XAML instead of MediaElement, because otherwise
            // CameraStreamSource.CloseMedia() does not seem to be called by the framework:/

            BackgroundVideoBrush.SetSource(_mediaElement);

            _cameraEffect.PreviousEffect();
            StatusTextBlock.Text = _cameraEffect.EffectName;

            AdjustOrientation();
        }

        private void Uninitialize()
        {
            StatusTextBlock.Text = "";

            if (_mediaElement != null)
            {
                _mediaElement.Source = null;
                _mediaElement = null;
            }

            if (_cameraStreamSource != null)
            {
                _cameraStreamSource.FrameRateChanged -= CameraStreamSource_FPSChanged;
                _cameraStreamSource = null;
            }

            _cameraEffect = null;

            if (_photoCaptureDevice != null)
            {
                _photoCaptureDevice.Dispose();
                _photoCaptureDevice = null;
            }
        }

        private void AboutMenuItem_Click(object sender, EventArgs e)
        {
            NavigationService.Navigate(new Uri("/AboutPage.xaml", UriKind.Relative));
        }

        private void NextButton_Click(object sender, EventArgs e)
        {
            _cameraEffect.NextEffect();

            StatusTextBlock.Text = _cameraEffect.EffectName;
        }

        private void PreviousButton_Click(object sender, EventArgs e)
        {
            _cameraEffect.PreviousEffect();

            StatusTextBlock.Text = _cameraEffect.EffectName;
        }

        private void CameraStreamSource_FPSChanged(object sender, int e)
        {
            FrameRateTextBlock.Text = String.Format(AppResources.MainPage_FrameRateTextBlock_Format, e);
            //StatusTextBlock.Text = string.Concat("QuantizeColorEffect with Cache count: ", App.AssignedColorCache.Count);
        }

        private async void LayoutRoot_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            if (_cameraSemaphore.WaitOne(100))
            {
                await _photoCaptureDevice.FocusAsync();

                _cameraSemaphore.Release();
            }
        }
    }
}