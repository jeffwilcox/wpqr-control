//
// Copyright (c) 2012 Jeff Wilcox
// Copyright (c) 2012 Michael Osthege
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//    http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using com.google.zxing;
using com.google.zxing.common;
using com.google.zxing.qrcode;
using Microsoft.Devices;

namespace JeffWilcox.Controls
{
    /// <summary>
    /// Represents a camera preview control that actively scans for a two-
    /// dimensional QR code.
    /// </summary>
    [TemplatePart(Name = VideoBrushName, Type = typeof(VideoBrush))]
    public class QRCodeScanner : Control
    {
        private static readonly Dictionary<object, object> QRCodeHint = new Dictionary<object, object>
        {
            { DecodeHintType.POSSIBLE_FORMATS, BarcodeFormat.QR_CODE }
        };

        private const string VideoBrushName = "VideoBrush";

        private QRCodeReader _reader;

        private VideoBrush _videoBrush;

        private PhotoCamera _photoCamera;

        private PhotoCameraLuminanceSource _luminanceSource;

        private bool _initialized;

        #region public bool IsScanning
        /// <summary>
        /// Gets a value indicating whether the code scanner is currently 
        /// scanning. It should not be set.
        /// </summary>
        public bool IsScanning
        {
            get { return (bool)GetValue(IsScanningProperty); }
            private set { SetValue(IsScanningProperty, value); }
        }

        /// <summary>
        /// Identifies the IsScanning dependency property.
        /// </summary>
        public static readonly DependencyProperty IsScanningProperty =
            DependencyProperty.Register(
                "IsScanning",
                typeof(bool),
                typeof(QRCodeScanner),
                new PropertyMetadata(false));
        #endregion public bool IsScanning

        /// <summary>
        /// Provides information about the scanned text when a scan is complete
        /// and scanning is stopped.
        /// </summary>
        public event EventHandler<ScanCompleteEventArgs> ScanComplete;

        /// <summary>
        /// Provides an exception object instance when an initialization or
        /// unhandled scanning error is captured.
        /// </summary>
        public event EventHandler<ScanFailureEventArgs> Error;

        /// <summary>
        /// Initializes a new instance of the QRCodeScanner control.
        /// </summary>
        public QRCodeScanner() : base()
        {
            DefaultStyleKey = typeof(QRCodeScanner);
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            _videoBrush = GetTemplateChild(VideoBrushName) as VideoBrush;

            if (_photoCamera == null && _videoBrush != null)
            {
                InitializePhotoCamera();
            }
        }

        private void InitializePhotoCamera()
        {
            _photoCamera = new PhotoCamera(CameraType.Primary);
            _photoCamera.Initialized += OnPhotoCameraInitialized;
            _videoBrush.SetSource(_photoCamera);
        }

        private void UninitializePhotoCamera()
        {
            _photoCamera.Initialized -= OnPhotoCameraInitialized;
            _photoCamera.Dispose();
            _photoCamera = null;
        }

        protected virtual void OnError(Exception ex)
        {
            var handler = Error;
            if (handler != null)
            {
                handler(this, new ScanFailureEventArgs(ex));
            }
        }

        private void OnPhotoCameraInitialized(object sender, CameraOperationCompletedEventArgs e)
        {
            if (e.Succeeded)
            {
                if (_videoBrush != null)
                {
                    Dispatcher.BeginInvoke(InitializeVideoBrush);
                }
            }
            else
            {
                OnError(e.Exception);
            }
        }

        private void InitializeVideoBrush()
        {
            var cr = _videoBrush.RelativeTransform as CompositeTransform;
            if (cr != null)
            {
                cr.Rotation = _photoCamera.Orientation;
            }

            try
            {
                _luminanceSource = new PhotoCameraLuminanceSource(
                    Convert.ToInt32(_photoCamera.PreviewResolution.Width),
                    Convert.ToInt32(_photoCamera.PreviewResolution.Height));
                _initialized = true;
                StartScanning();
            }
            catch (Exception ex)
            {
                OnError(ex);
            }
        }

        /// <summary>
        /// Starts or restarts actively scanning the camera's preview buffer
        /// for a two-dimensional barcode.
        /// </summary>
        public void StartScanning()
        {
            if (!IsScanning && _initialized)
            {
                _reader = new QRCodeReader();
                IsScanning = true;
                Scan();
            }
        }

        public void StopScanning()
        {
            if (IsScanning)
            {
                IsScanning = false;
                _reader = null;				
                UninitializePhotoCamera();
            }
        }

        protected virtual void OnResult(Result result)
        {
            if (result != null)
            {
                var handler = ScanComplete;
                if (handler != null)
                {
                    handler(this, new ScanCompleteEventArgs(result.Text));
                }
            }
        }

        private void Scan()
        {
            if (IsScanning && _initialized && _photoCamera != null && _luminanceSource != null && _reader != null)
            {
                try
                {
                    // 2-2-2012 - Rowdy.nl
                    // Focus the camera for better recognition of QR code's
                    if (_photoCamera.IsFocusSupported)
                    {
                        _photoCamera.Focus();
                    }
                    // End Rowdy.nl
                    
                    _photoCamera.GetPreviewBufferY(_luminanceSource.PreviewBufferY);
                    var binarizer = new HybridBinarizer(_luminanceSource);
                    var binaryBitmap = new BinaryBitmap(binarizer);
                    var result = _reader.decode(binaryBitmap, QRCodeHint);

                    StopScanning();
                    OnResult(result);
                }
                catch (ReaderException)
                {
                    // There was not a successful QR code read in the scan 
                    // pass. Invoke and try again soon.
                    Dispatcher.BeginInvoke(Scan);
                }
                catch (Exception ex)
                {
                    OnError(ex);
                }
            }
        }
    }
}
