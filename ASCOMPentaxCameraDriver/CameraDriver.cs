//tabs=4
// --------------------------------------------------------------------------------
//
// ASCOM Camera driver for Pentax KP Camera
//
// Description:	Implements ASCOM driver for Pentax KP camera.
//				Communicates using USB connection.
//
// Implements:	ASCOM Camera interface version: 2
// Author:		(2019) Doug Henderson <retrodotkiwi@gmail.com>
//
// Edit Log:
//
// Date			Who	Vers	Description
// -----------	---	-----	-------------------------------------------------------
// 10-Dec-2019	XXX	6.0.0	Initial edit, created from ASCOM driver template
// --------------------------------------------------------------------------------
//
#define Camera

using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Text;
using System.Runtime.InteropServices;
using ASCOM;
using ASCOM.Astrometry;
using ASCOM.Astrometry.AstroUtils;
using ASCOM.Utilities;
using ASCOM.DeviceInterface;
using System.Globalization;
using System.Collections;
using System.Threading;
using Microsoft.Win32;
using System.IO;
using Ricoh.CameraController;
using System.Windows.Media.Imaging;
using System.Diagnostics.Tracing;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Imaging;
using ASCOM.DSLR.Classes;
using System.Collections.Concurrent;

namespace ASCOM.PentaxKP
{
    //
    // Your driver's DeviceID is ASCOM.PentaxKP.Camera
    //
    // The Guid attribute sets the CLSID for ASCOM.PentaxKP.Camera

    // TODO Replace the not implemented exceptions with code to implement the function or
    // throw the appropriate ASCOM exception.
    //

    /// <summary>
    /// ASCOM Camera Driver for Pentax KP Camera.
    /// </summary>
    [Guid("528fb38b-ed8c-456e-a6b8-cde4c1533aa2")]
    [ClassInterface(ClassInterfaceType.None)]
    public class Camera : ICameraV2
    {
        /// <summary>
        /// Private variable to hold an ASCOM Utilities object
        /// </summary>
        private Util utilities;

        /// <summary>
        /// Private variable to hold an ASCOM AstroUtilities object to provide the Range method
        /// </summary>
        private AstroUtils astroUtilities;

        /// <summary>
        /// Variable to hold the trace logger object (creates a diagnostic log file with information that you specify)
        /// </summary>

        internal static bool LastSetFastReadout = false;
        internal static int RequestedStartX = 0;
        internal static int RequestedStartY = 0;
        internal static int RequestedWidth = 6016;
        internal static int RequestedHeight = 4000;
        internal static int DefaultImageWidthPixels = 6016;
        internal static int DefaultImageHeightPixels = 4000;
                                                           //        internal static ConcurrentQueue<String> imagesToProcess;
        internal static Queue<String> imagesToProcess;
        internal static Queue<BitmapImage> bitmapsToProcess;
        private ImageDataProcessor _imageDataProcessor;
        // Index to the current ISO level
        internal short gainIndex=0;
        // The different ISO levels
        internal ArrayList m_gains;

        // Two output modes 0=6016x4000 standard and 1=720x480 liveview
        internal static int m_readoutmode=0;

        // If saving in raw formate for standard output mode
        internal static bool m_rawmode = true;
//        internal static bool m_rawmode = false;

        internal Thread cameraThread;

        internal Ricoh.CameraController.CaptureState m_captureState = Ricoh.CameraController.CaptureState.Unknown;

        class EventListener : CameraEventListener
        {
            public override void LiveViewFrameUpdated(
                CameraDevice sender,
                byte[] liveViewFrame)
            {
                // Display liveViewFrame in Image control (Name: image) of WPF
                var memoryStream = new MemoryStream(liveViewFrame);
                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memoryStream;
                bitmapImage.EndInit();
                bitmapsToProcess.Enqueue(bitmapImage);
                DriverCommon.LogCameraMessage("", "Received LiveView Image");
            }

            // Image Added
            public override void ImageAdded(CameraDevice sender, CameraImage image)
            {
                // Get the image and save it in the current directory
                using (FileStream fs = new FileStream(
                    "c:/users/richr" + Path.DirectorySeparatorChar +
                    image.Name, FileMode.Create, FileAccess.Write))
                {
                    Response imageGetResponse = image.GetData(fs);
                    DriverCommon.LogCameraMessage("","Get Image is " +
                        (imageGetResponse.Result == Result.OK ?
                            "SUCCEED." : "FAILED."));
                    DriverCommon.LogCameraMessage("", "c:/users/richr" + Path.DirectorySeparatorChar +
                    image.Name);
                    imagesToProcess.Enqueue("c:/users/richr" + Path.DirectorySeparatorChar + image.Name);
                    
                }
            }

            // Capture Complete
            public override void CaptureComplete(CameraDevice sender, Capture capture)
            {
                DriverCommon.LogCameraMessage("","Capture Complete. Capture ID: "+capture.ID.ToString());
            }

            public override void DeviceDisconnected(CameraDevice sender, Ricoh.CameraController.DeviceInterface deviceInterface)
            {
                //Fix
                //StopThreadCapture();
                DriverCommon.LogCameraMessage("","Device Disconnected.");
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PentaxKP"/> class.
        /// Must be public for COM registration.
        /// </summary>
        public Camera()
        {
            imagesToProcess = new Queue<string>();
            bitmapsToProcess = new Queue<BitmapImage>();
            m_gains = new ArrayList();
            m_gains.Add("ISO 100");
            m_gains.Add("ISO 200");
            m_gains.Add("ISO 400");
            m_gains.Add("ISO 800");
            m_gains.Add("ISO 1600");
            m_gains.Add("ISO 3200");

            _imageDataProcessor = new ImageDataProcessor();

            DriverCommon.ReadProfile(); // Read device configuration from the ASCOM Profile store

            DriverCommon.LogCameraMessage("Camera", "Starting initialisation");

            utilities = new Util(); //Initialise util object
            astroUtilities = new AstroUtils(); // Initialise astro utilities object

            DriverCommon.LogCameraMessage("Camera", "Completed initialisation");

        }

        private void ReadProfile()
        {
			// What is this?
            DriverCommon.ReadProfile(); // Read device configuration from the ASCOM Profile store
        }

        //
        // PUBLIC COM INTERFACE ICameraV2 IMPLEMENTATION
        //

        #region Common properties and methods.

        /// <summary>
        /// Displays the Setup Dialog form.
        /// If the user clicks the OK button to dismiss the form, then
        /// the new settings are saved, otherwise the old values are reloaded.
        /// THIS IS THE ONLY PLACE WHERE SHOWING USER INTERFACE IS ALLOWED!
        /// </summary>
        public void SetupDialog()
        {
            // consider only showing the setup dialog if not connected
            // or call a different dialog if connected
            //            if (IsConnected)
            //                System.Windows.Forms.MessageBox.Show("Camera is currently connected.  Some options are only available when not connected, these will be disabled.");
            DriverCommon.LogCameraMessage("SetupDialog", "[in]");

            using (SetupDialogForm F = new SetupDialogForm())
            {
                var result = F.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    DriverCommon.WriteProfile(); // Persist device configuration values to the ASCOM Profile store

                    // Update connected camera with bulb mode info
                    if (DriverCommon.Camera != null)
                    {
                        DriverCommon.Camera.BulbMode = DriverCommon.Settings.BulbModeEnable;
                        DriverCommon.Camera.BulbModeTime = DriverCommon.Settings.BulbModeTime;
                    }
                }
            }

            DriverCommon.LogCameraMessage("SetupDialog", "[out]");
        }

        public ArrayList SupportedActions
        {
            get
            {
                DriverCommon.LogCameraMessage("SupportedActions Get", "Returning empty arraylist");
                return new ArrayList();
            }
        }

        public string Action(string actionName, string actionParameters)
        {
            DriverCommon.LogCameraMessage("", $"Action {actionName}, parameters {actionParameters} not implemented");
            throw new ASCOM.ActionNotImplementedException("Action " + actionName + " is not implemented by this driver");
        }

        public void CommandBlind(string command, bool raw)
        {
            DriverCommon.LogCameraMessage("", $"CommandBlind {command} not implemented");
            throw new ASCOM.MethodNotImplementedException("CommandBlind");
        }

        public bool CommandBool(string command, bool raw)
        {
            DriverCommon.LogCameraMessage("", $"CommandBool {command} not implemented");
            throw new ASCOM.MethodNotImplementedException("CommandBool");
        }

        public string CommandString(string command, bool raw)
        {
            DriverCommon.LogCameraMessage("", $"CommandString {command} not implemented");
            throw new ASCOM.MethodNotImplementedException("CommandString");
        }

        public void Dispose()
        {
            DriverCommon.LogCameraMessage("Dispose", "Disposing");
			Connected = false;
            utilities.Dispose();
            utilities = null;
            astroUtilities.Dispose();
            astroUtilities = null;
           // tl.Enabled = false;
           // tl.Dispose();
           // tl = null;
        }

        public bool Connected
        {
            get
            {
                using (new DriverCommon.SerializedAccess("get_Connected"))
                {
                    DriverCommon.LogCameraMessage("Connected", "get");
                    if (DriverCommon.m_camera == null)
                        return false;

                    return DriverCommon.m_camera.IsConnected(Ricoh.CameraController.DeviceInterface.USB);
				}
            }
            set
            {
                using (new DriverCommon.SerializedAccess("set_Connected", false))
                {
                    DriverCommon.LogCameraMessage("", $"set_Connected Set {value.ToString()}");
                    if (value)
                    {
	                    if (!DriverCommon.CameraConnected && DriverCommon.Settings.DeviceId == "")
	                    {
						//Fix!!!!
	                        // Need to display setup dialog
	                        SetupDialog();
	                    }
						
                        if (DriverCommon.m_camera == null)
                        {
                            DriverCommon.LogCameraMessage("Connected", "Connecting...");
                            List<CameraDevice> detectedCameraDevices = CameraDeviceDetector.Detect(Ricoh.CameraController.DeviceInterface.USB);
                            DriverCommon.m_camera = detectedCameraDevices.First();
                            if (DriverCommon.m_camera != null)
                            {
                                var response = DriverCommon.m_camera.Connect(Ricoh.CameraController.DeviceInterface.USB);
                                if (response.Equals(Response.OK))
                                {
                                    DriverCommon.LogCameraMessage("Connected", "Connected. Model: " + DriverCommon.m_camera.Model + ", SerialNumber:" + DriverCommon.m_camera.SerialNumber);
                                    StorageWriting sw = new StorageWriting();
                                    sw=Ricoh.CameraController.StorageWriting.False;
                                    StillImageCaptureFormat sicf = new StillImageCaptureFormat();

                                    sicf = Ricoh.CameraController.StillImageCaptureFormat.JPEG;
                                    if(m_rawmode)
                                        sicf = Ricoh.CameraController.StillImageCaptureFormat.DNG;
                                    StillImageQuality siq = new StillImageQuality();
                                    siq=Ricoh.CameraController.StillImageQuality.LargeBest;
                                    DriverCommon.m_camera.SetCaptureSettings(new List<CaptureSetting>() { sw });
                                    DriverCommon.m_camera.SetCaptureSettings(new List<CaptureSetting>() { siq });
                                    DriverCommon.m_camera.SetCaptureSettings(new List<CaptureSetting>() { sicf });
                                    // Sleep to let the settings take effect
                                    Thread.Sleep(1000);

                                    Gain = gainIndex;
                                }
                                else
                                {
                                    DriverCommon.LogCameraMessage("Connected", "Connection is failed.");
                                }



                                if (DriverCommon.m_camera.EventListeners.Count == 0)
                                    DriverCommon.m_camera.EventListeners.Add(new EventListener());

                                /*response = DriverCommon.m_camera.StartLiveView();
                                if (response.Equals(Response.OK))
                                {
                                    LastSetFastReadout = true;
                                    DriverCommon.LogCameraMessage("Connected", "Live View Enabled");
                                }
                                else
                                {
                                    LastSetFastReadout = false;
                                    DriverCommon.LogCameraMessage("Connected", "Live View Failed.");
                                }*/
                            }
                            else
                            {
                                DriverCommon.LogCameraMessage("Connected", "Device has not found.");
                            }
                        }
                    }
                    else
                    {
                        if (DriverCommon.m_camera != null)
                        {
                            // Stop the capture if necessary
                            StopThreadCapture();
                            DriverCommon.m_camera.Disconnect(Ricoh.CameraController.DeviceInterface.USB);
                        }

                        DriverCommon.m_camera = null;
                        DriverCommon.LogCameraMessage("Connected", "Closed connection to camera");
                    }
                }
            }
        }

        public string Description
        {
            get
            {
                using (new DriverCommon.SerializedAccess("get_Description"))
                {
                    DriverCommon.LogCameraMessage("", "get_Description");
//                    return DriverCommon.m_camera.Model;
	                return DriverCommon.CameraDriverDescription;
                }
            }
        }

        public string DriverInfo
        {
            get
            {
                DriverCommon.LogCameraMessage("", "get_DriverInfo");
                return DriverCommon.CameraDriverInfo;
            }
        }

        public string DriverVersion
        {
            get
            {
                DriverCommon.LogCameraMessage("", "get_DriverVersion");
                return DriverCommon.DriverVersion;
            }
        }

        public short InterfaceVersion
        {
            get
            {
                DriverCommon.LogCameraMessage("", "get_InterfaceVersion");
                return Convert.ToInt16("2");
            }
        }

        public string Name
        {
            get
            {
                DriverCommon.LogCameraMessage("", "get_Name");
                return DriverCommon.CameraDriverName;
            }
        }

        #endregion

        #region ICamera Implementation

        public void AbortExposure()
        {
            DriverCommon.LogCameraMessage("", "AbortExposure");
            StopThreadCapture();
            if (LastSetFastReadout)
            {
                //No need to start exposure
                DriverCommon.LogCameraMessage("", "AbortExposure() fast");
                m_captureState = Ricoh.CameraController.CaptureState.Executing;
            }

        }

        public short BayerOffsetX
        {
            get
            {
                //using (new SerializedAccess(this, "get_BayerOffsetX"))
                {
                    DriverCommon.LogCameraMessage("", "get_BayerOffsetX");
                    return 0;
                }
            }
        }

        public short BayerOffsetY
        {
            get
            {
                //using (new SerializedAccess(this, "get_BayerOffsetY"))
                {
                    DriverCommon.LogCameraMessage("", "get_BayerOffsetY");
                    return 0;
                }
            }
        }

        public short BinX
        {
            get
            {
                //using (new SerializedAccess(this, "get_BinX"))
                {
                    DriverCommon.LogCameraMessage("", "get_BinX");
                    return 1;
                }

            }
            set
            {
                //using (new SerializedAccess(this, "set_BinX"))
                {
                    DriverCommon.LogCameraMessage("", "set_BinX");
                    if (value != 1) throw new ASCOM.InvalidValueException("BinX", value.ToString(), "1"); // Only 1 is valid in this simple template
                }
            }
        }

        public short BinY
        {
            get
            {
                //using (new SerializedAccess(this, "get_BinY"))
                {
                    DriverCommon.LogCameraMessage("", "get_BinY");
                    return 1;
                }
            }
            set
            {
                //using (new SerializedAccess(this, "set_BinY"))
                {
                    DriverCommon.LogCameraMessage("", "set_BinY");
                    if (value != 1) throw new ASCOM.InvalidValueException("BinY", value.ToString(), "1"); // Only 1 is valid in this simple template
				}
            }
        }

        public double CCDTemperature
        {
            get
            {
                //using (new SerializedAccess(this, "get_CCDTemperature", true))
                {
                    DriverCommon.LogCameraMessage("", "get_CCDTemperature");
                   double temperature = 5;
                   return temperature;
				}
            }
        }

        public CameraStates CameraState
        {
            get
            {
                //using (new SerializedAccess(this, "get_CameraState", true))
                {
                    DriverCommon.LogCameraMessage("", $"get_CameraState {m_captureState.ToString()}");
                    //Fix!!!!
                    switch (m_captureState)
                    {
                        case Ricoh.CameraController.CaptureState.Executing:
                            return CameraStates.cameraExposing;

                        case Ricoh.CameraController.CaptureState.Complete:
                            return CameraStates.cameraIdle;

                        default:
                            return CameraStates.cameraIdle;
                    }
                }
            }
        }

        public int CameraXSize
        {
            get
            {
                //using (new SerializedAccess(this, "get_CameraXSize", true))
                {
                    DriverCommon.LogCameraMessage("", "get_CameraXSize");
                    return DefaultImageWidthPixels;
				}
            }
        }

        public int CameraYSize
        {
            get
            {
                //using (new SerializedAccess(this, "get_CameraYSize", true))
                {
                    DriverCommon.LogCameraMessage("", "get_CameraYSize");
                    return DefaultImageHeightPixels;
				}
            }
        }

        public bool CanAbortExposure
        {
            get
            {
                //using (new SerializedAccess(this, "get_CanAbortExposure"))
                {
                    DriverCommon.LogCameraMessage("", "get_CanAbortExposure");
                    return true;
				}
            }
        }

        public bool CanAsymmetricBin
        {
            get
            {
                //using (new SerializedAccess(this, "get_CanAsymmetricBin"))
                {
                    DriverCommon.LogCameraMessage("", "get_CanAsymmetricBin");
                    return false;
				}
            }
        }

        public bool CanFastReadout
        {
            get
            {
                //using (new SerializedAccess(this, "get_CanFastReadout"))
                {
                    DriverCommon.LogCameraMessage("", "get_CanFastReadout");
                    return true;
				}
            }
        }

        public bool CanGetCoolerPower
        {
            get
            {
               //using (new SerializedAccess(this, "get_CanGetCoolerPower"))
                {
                    DriverCommon.LogCameraMessage("", "get_CanGetCoolerPower");
                    return false;
				}
            }
        }

        public bool CanPulseGuide
        {
            get
            {
               //using (new SerializedAccess(this, "get_CanPulseGuide"))
                {
                    DriverCommon.LogCameraMessage("", "get_CanPulseGuide");
                    return false;
				}
            }
        }

        public bool CanSetCCDTemperature
        {
            get
            {
                //using (new SerializedAccess(this, "get_CanSetCCDTemperature"))
                {
                    DriverCommon.LogCameraMessage("", "get_CanSetCCDTemperature");
                    return false;
				}
            }
        }

        public bool CanStopExposure
        {
            get
            {
                //using (new SerializedAccess(this, "get_CanStopExposure"))
                {
                    DriverCommon.LogCameraMessage("", "get_CanStopExposure");
                    return true;
				}
            }
        }

        public bool CoolerOn
        {
            get
            {
                //using (new SerializedAccess(this, "get_CoolerOn"))
                {
                    DriverCommon.LogCameraMessage("", "get_CoolerOn");
                    throw new ASCOM.PropertyNotImplementedException("CoolerOn", false);
				}
            }
            set
            {
                //using (new SerializedAccess(this, "set_CoolerOn"))
                {
                    DriverCommon.LogCameraMessage("", "set_CoolerOn");
                    throw new ASCOM.PropertyNotImplementedException("CoolerOn", true);
				}
            }
        }

        public double CoolerPower
        {
            get
            {
                //using (new SerializedAccess(this, "get_CoolerPower"))
                {
                    DriverCommon.LogCameraMessage("", "get_CoolerPower");
                    throw new ASCOM.PropertyNotImplementedException("CoolerPower", false);
				}
            }
        }

        public double ElectronsPerADU
        {
            get
            {
                //using (new SerializedAccess(this, "get_ElectronsPerADU"))
                {
                    DriverCommon.LogCameraMessage("", "get_ElectronsPerADU");
                    throw new ASCOM.PropertyNotImplementedException("ElectronsPerADU", false);
				}
            }
        }

        public double ExposureMax
        {
            get
            {
            // Maximum exposure time
                //using (new SerializedAccess(this, "get_ExposureMax", true))
                {
                    DriverCommon.LogCameraMessage("", "get_ExposureMax");
                    return 30;
				}
            }
        }

        public double ExposureMin
        {
            get
            {
            // Minimum exposure time
                //using (new SerializedAccess(this, "get_ExposureMin", true))
                {
                    DriverCommon.LogCameraMessage("", "get_ExposureMin");
                    return 0.001/5.0;
				}
            }
        }

        public double ExposureResolution
        {
            get
            {
                //using (new SerializedAccess(this, "get_ExposureResolution", true))
                {
                    DriverCommon.LogCameraMessage("", "get_ExposureResolution");
                    return 0.001/5.0;
				}
            }
        }

        public bool FastReadout
        {
            get
            {
                //using (new SerializedAccess(this, "get_FastReadout"))
                {
                    DriverCommon.LogCameraMessage("", "get_FastReadout");
                    return LastSetFastReadout;
				}
            }
            set
            {
                using (new DriverCommon.SerializedAccess("set_FastReadout",false))
                {
                    DriverCommon.LogCameraMessage("", "set_FastReadout");
                    if (LastSetFastReadout)
                    {
                        if (!value)
                            DriverCommon.m_camera.StopLiveView();
                        else
                            StopExposure();
                    }
                    if (value)
                        DriverCommon.m_camera.StartLiveView();
                    LastSetFastReadout = value;
/*                    if (value)
                        ReadoutMode = 1;
                    else
                        ReadoutMode = 0;*/
				}
            }
        }

        public double FullWellCapacity
        {
            get
            {
                //using (new SerializedAccess(this, "get_FullWellCapacity"))
                {
                    DriverCommon.LogCameraMessage("", "get_FullWellCapacity");
                    throw new ASCOM.PropertyNotImplementedException("FullWellCapacity", false);
				}
            }
        }

        public short Gain
        {
            get
            {
                using (new DriverCommon.SerializedAccess("get_Gain"))
                {
                    DriverCommon.LogCameraMessage("", "get_Gain");
                    return gainIndex;
				}
            }

            set
            {
               using (new DriverCommon.SerializedAccess("set_Gain"))
                {
                    // Check connected
                    DriverCommon.LogCameraMessage("", "set_Gain "+value.ToString());
                    gainIndex = value;
                    if (gainIndex < 0)
                        gainIndex = 0;
                    if (gainIndex > 5)
                        gainIndex = 5;
                    using (new DriverCommon.SerializedAccess("get_Gain"))
                    {
                        // Can I set this any time? Fix
                        if (DriverCommon.m_camera != null)
                        {
                            ISO iso = new ISO();
                            if(gainIndex==0)
                                iso = ISO.ISO100;
                            if (gainIndex == 1)
                                iso = ISO.ISO200;
                            if (gainIndex == 2)
                                iso = ISO.ISO400;
                            if (gainIndex == 3)
                                iso = ISO.ISO800;
                            if (gainIndex == 4)
                                iso = ISO.ISO1600;
                            if (gainIndex == 5)
                                iso = ISO.ISO3200;
                            DriverCommon.m_camera.SetCaptureSettings(new List<CaptureSetting>() { iso });
                        }
                        else
                            throw new ASCOM.PropertyNotImplementedException("GainMax", false);
                    }
                }
            }
        }

        public short GainMax
        {
            get
            {
//                using (new DriverCommon.SerializedAccess("get_GainMax"))
                {
                    DriverCommon.LogCameraMessage("", "get_GainMax");
//                    return 5;
                    throw new ASCOM.PropertyNotImplementedException("GainMax", false);
				}
            }
        }

        public short GainMin
        {
            get
            {
                using (new DriverCommon.SerializedAccess("get_GainMin"))
                {
                    DriverCommon.LogCameraMessage("", "get_GainMin");
//                    return 0;
                    throw new ASCOM.PropertyNotImplementedException("GainMin", true);
				}
            }
        }

        public ArrayList Gains
        {
            get
            {
//               using (new DriverCommon.SerializedAccess("get_Gains"))
               {
                    DriverCommon.LogCameraMessage("", "get_Gains");
                    return m_gains;
            	}
            }
        }

        public bool HasShutter
        {
            get
            {
                //using (new SerializedAccess(this, "get_HasShutter"))
                {
                    DriverCommon.LogCameraMessage("", "get_HasShutter");
                    return true;
				}
            }
        }

        public double HeatSinkTemperature
        {
            get
            {
                DriverCommon.LogCameraMessage("", "get_HeatSinkTemperature");
                throw new ASCOM.PropertyNotImplementedException("HeatSinkTemperature", false);
            }
        }
/*
        private object ImageData()
        {
            DriverCommon.LogCameraMessage("", "ImageData()");

            object result = null;

            // This is where the magic happens - the Image needs to be converted to RGB

            if (DriverCommon.Camera.LastImage.Status != PentaxKPImage.ImageStatus.Ready)
            {
                DriverCommon.LogCameraMessage("ImageArray Get", "Throwing InvalidOperationException because of a call to ImageArray before the first image has been taken!");
                throw new ASCOM.InvalidOperationException("Call to ImageArray before the first image has been taken!");
            }

            PentaxKPImage image = DriverCommon.Camera.LastImage;

            int requestedWidth = NumX;
            int requestedHeight = NumY;
            DriverCommon.LogCameraMessage("ImageArray", "requestedWidth = {0}, cameraNumX = {1}, camera.Mode.ImageWidth = {2}", (int)Math.Min(DriverCommon.Settings.ImageWidth, DriverCommon.Camera.Mode.ImageWidthPixels), DriverCommon.Settings.ImageWidth, DriverCommon.Camera.Mode.ImageWidthPixels);
            DriverCommon.LogCameraMessage("ImageArray Get", String.Format("(numX = {0}, numY = {1}, image.Width = {2}, image.Height = {3})", requestedWidth, requestedHeight, image.Width, image.Height));

            switch (image.m_info.ImageMode)
            {
                case 1:
                    DriverCommon.LogCameraMessage("BAYER info", String.Format("Dimensions = {0}, {1} x {2}", PentaxKPImage.BAYER.Rank, PentaxKPImage.BAYER.GetLength(0), PentaxKPImage.BAYER.GetLength(1)));

                    result = Resize(PentaxKPImage.BAYER, PentaxKPImage.BAYER.Rank, StartX, StartY, requestedWidth, requestedHeight);
                    break;

                case 2:
                    if (DriverCommon.Settings.Personality != PentaxKPCommon.PERSONALITY_NINA)
                    {
                        DriverCommon.LogCameraMessage("RGB info", String.Format("Dimensions = {0}, {1} x {2} x {3}", PentaxKPImage.RGB.Rank, PentaxKPImage.RGB.GetLength(0), PentaxKPImage.RGB.GetLength(1), PentaxKPImage.RGB.GetLength(2)));

                        result = Resize(PentaxKPImage.RGB, PentaxKPImage.RGB.Rank, StartX, StartY, requestedWidth, requestedHeight);
                    }
                    else
                    {
                        DriverCommon.LogCameraMessage("RGB info as MONO", String.Format("Dimensions = {0}, {1} x {2}", PentaxKPImage.BAYER.Rank, PentaxKPImage.BAYER.GetLength(0), PentaxKPImage.BAYER.GetLength(1)));

                        result = Resize(PentaxKPImage.BAYER, PentaxKPImage.BAYER.Rank, StartX, StartY, requestedWidth, requestedHeight);
                    }
                    break;

                default:
                    DriverCommon.LogCameraMessage("Unknown info", String.Format("{0} - Throwing", image.m_info.ImageMode));

                    throw new ASCOM.InvalidOperationException("Call to ImageArray resulted in invalid image type!");
            }

            return result;
        }
*/
        private Bitmap BitmapImage2Bitmap(BitmapImage bitmapImage)
        {
            // BitmapImage bitmapImage = new BitmapImage(new Uri("../Images/test.png", UriKind.Relative));

            using (MemoryStream outStream = new MemoryStream())
            {
                BitmapEncoder enc = new BmpBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(bitmapImage));
                enc.Save(outStream);
                System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(outStream);

                return new Bitmap(bitmap);
            }
        }

        private object ReadImageFileRaw(string MNewFile)
        {
            object result = null;
            //Bitmap _bmp;
            int MSensorWidthPx = 6016;
            int MSensorHeightPx = 4000;
            int[,,] _cameraImageArray= new int[MSensorWidthPx, MSensorHeightPx, 3]; // Assuming this is declared and initialized elsewhere.
            int[,] bayerData;//=new int[MSensorHeightPx*2,MSensorHeightPx*2];


            // Wait for the file to be closed and available.
            while (!IsFileClosed(MNewFile)) { }
            bayerData = _imageDataProcessor.ReadRaw(MNewFile);

            int height = 4000;
            int width = 6016;

            // RGGB

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int r = 0, g = 0, b = 0;

                    if (y % 2 == 0) // Even row
                    {
                        if (x % 2 == 0) // Even column (Red pixel)
                        {
                            r = bayerData[x,y];
                            g = (bayerData[x + 1, y] +
                                 bayerData[x, y + 1]) / 2;
                            b = bayerData[x + 1, y + 1];
                        }
                        else // Odd column (Green pixel)
                        {
                            g = bayerData[x,y];
                            if(x==0)
                                r = bayerData[x + 1, y];
                            else
                                r = (bayerData[x - 1, y] +
                                     bayerData[x + 1, y]) / 2;
                            b = bayerData[x, y + 1];
                        }
                    }
                    else // Odd row
                    {
                        if (x % 2 == 0) // Even column (Green pixel)
                        {
                            g = bayerData[x,y];
                            r = bayerData[x, y - 1];
                            if(x==0)
                                b = bayerData[x + 1, y] ;
                            else
                                b = (bayerData[x + 1, y] +
                                     bayerData[x - 1, y]) / 2;
                        }
                        else // Odd column (Blue pixel)
                        {
                            b = bayerData[x,y];
                            if(x==0)
                                g = bayerData[x, y - 1];
                            else
                            g = (bayerData[x - 1, y] +
                                 bayerData[x, y - 1]) / 2;
                            if (x == 0)
                                r = 0;
                            else
                                r = bayerData[x - 1, y - 1];
                        }
                    }

                    _cameraImageArray[x, y, 0] = b;
                    _cameraImageArray[x, y, 1] = g;
                    _cameraImageArray[x, y, 2] = r;
                }
            }


        //_cameraImageArray = _imageDataProcessor.ReadAndDebayerRaw(MNewFile);
        result = Resize(_cameraImageArray, 3, StartX, StartY, NumX, NumY);
            return result;
        }

        private object ReadImageFileQuick(string MNewFile)
        {
            object result = null;
            Bitmap _bmp;
            int MSensorWidthPx = 6016;
            int MSensorHeightPx = 4000;
            int[,,] _cameraImageArray= new int[MSensorWidthPx, MSensorHeightPx, 3]; // Assuming this is declared and initialized elsewhere.

            // Wait for the file to be closed and available.
                while (!IsFileClosed(MNewFile)) { }

                _bmp = (Bitmap)Image.FromFile(MNewFile); // Load the newly discovered file

                // Lock the bitmap's bits.
                Rectangle rect = new Rectangle(0, 0, _bmp.Width, _bmp.Height);
                BitmapData bmpData = _bmp.LockBits(rect, ImageLockMode.ReadWrite, _bmp.PixelFormat);

                IntPtr ptr = bmpData.Scan0;

                int stride = bmpData.Stride;
                int width = _bmp.Width;
                int height = _bmp.Height;
            
            for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        _cameraImageArray[x, y, 0] = Marshal.ReadByte(ptr, (stride * y) + (3 * x))*1;
                        _cameraImageArray[x, y, 1] = Marshal.ReadByte(ptr + 1, (stride * y) + (3 * x))*1;
                        _cameraImageArray[x, y, 2] = Marshal.ReadByte(ptr + 2, (stride * y) + (3 * x))*1;
                    }
                }
 
            // Unlock the bits.
            _bmp.UnlockBits(bmpData);
                result = Resize(_cameraImageArray, 3, StartX, StartY, NumX, NumY);
            return result;
        }

        private bool IsFileClosed(string filePath)
        {
            try
            {
                using (var stream = new System.IO.FileStream(filePath, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.None))
                {
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        private object ReadImageQuick(BitmapImage image)
        {
            object result = null;
            Bitmap _bmp=BitmapImage2Bitmap(image);
           // Lock the bitmap's bits.
            Rectangle rect = new Rectangle(0, 0, _bmp.Width, _bmp.Height);
            BitmapData bmpData = _bmp.LockBits(rect, ImageLockMode.ReadWrite, _bmp.PixelFormat);
            int[,,] _cameraImageArray = new int[_bmp.Width, _bmp.Height, 3]; // Assuming this is declared and initialized elsewhere.


            IntPtr ptr = bmpData.Scan0;

            int stride = bmpData.Stride;
            int width = _bmp.Width;
            int height = _bmp.Height;

            //Format32BppArgb Given X and Y coordinates,  the address of the first element in the pixel is Scan0+(y * stride)+(x*4).
            //This Points to the blue byte. The following three bytes contain the green, red and alpha bytes.

            //Format24BppRgb Given X and Y coordinates, the address of the first element in the pixel is Scan0+(y*Stride)+(x*3). 
            //This points to the blue byte which is followed by the green and the red.

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    _cameraImageArray[x, y, 0] = Marshal.ReadByte(ptr, (stride * y) + (4 * x));
                    _cameraImageArray[x, y, 1] = Marshal.ReadByte(ptr + 1, (stride * y) + (4 * x));
                    _cameraImageArray[x, y, 2] = Marshal.ReadByte(ptr + 2, (stride * y) + (4 * x));
                }
            }

            // Unlock the bits.
            _bmp.UnlockBits(bmpData);
            result = Resize(_cameraImageArray, 3, StartX, StartY, _bmp.Width, _bmp.Height);
            return result;
        }

        public object ImageArray
        {
            get
            {
//                using (new SerializedAccess(this, "get_ImageArray",false))
                {
                    object result;

                    DriverCommon.LogCameraMessage("", "get_ImageArray");
                    String imageName;
                    BitmapImage bitmap;
                    if (bitmapsToProcess.Count != 0)
                    {
                        bitmap = bitmapsToProcess.Dequeue();
                        DriverCommon.LogCameraMessage("", "Calling ReadImageQuick");

                        result=ReadImageQuick(bitmap);
                        return result;
                    }

                    if (imagesToProcess.Count != 0)
                    {
                        imageName = imagesToProcess.Dequeue();
                        if (imageName.Substring(imageName.Length - 3) == "JPG")
                        {
                            DriverCommon.LogCameraMessage("", "Calling ReadImageFileQuick");
                            result = ReadImageFileQuick(imageName);
                            return result;
                        }

                        if (imageName.Substring(imageName.Length - 3) == "DNG")
                        {
                            DriverCommon.LogCameraMessage("", "Calling ReadImageFileRAW");
                            result = ReadImageFileRaw(imageName);
                            return result;
                        }

                        throw new ASCOM.PropertyNotImplementedException("ImageArray", false);
                    }

                    throw new ASCOM.PropertyNotImplementedException("ImageArray", false);

                    /*object result = null;

                    if (DriverCommon.Camera.LastImage.Status != PentaxKPImage.ImageStatus.Ready)
                    {
                        DriverCommon.LogCameraMessage("ImageArray Get", "Throwing InvalidOperationException because of a call to ImageArray before the first image has been taken!");
                        throw new ASCOM.InvalidOperationException("Call to ImageArray before the first image has been taken!");
                    }

                    PentaxKPImage image = DriverCommon.Camera.LastImage;

                    int requestedWidth = NumX;
                    int requestedHeight = NumY;
                    DriverCommon.LogCameraMessage("ImageArray", "requestedWidth = {0}, cameraNumX = {1}, camera.Mode.ImageWidth = {2}", (int)Math.Min(DriverCommon.Settings.ImageWidth, DriverCommon.Camera.Mode.ImageWidthPixels), DriverCommon.Settings.ImageWidth, DriverCommon.Camera.Mode.ImageWidthPixels);
                    DriverCommon.LogCameraMessage("ImageArray Get", String.Format("(numX = {0}, numY = {1}, image.Width = {2}, image.Height = {3})", requestedWidth, requestedHeight, image.Width, image.Height));

                    switch (image.m_info.ImageMode)
                    {
                        case 1:
                            DriverCommon.LogCameraMessage("BAYER info", String.Format("Dimensions = {0}, {1} x {2}", PentaxKPImage.BAYER.Rank, PentaxKPImage.BAYER.GetLength(0), PentaxKPImage.BAYER.GetLength(1)));

                            result = Resize(PentaxKPImage.BAYER, PentaxKPImage.BAYER.Rank, StartX, StartY, requestedWidth, requestedHeight);
                            break;

                        case 2:
                            if (DriverCommon.Settings.Personality != PentaxKPCommon.PERSONALITY_NINA)
                            {
                                DriverCommon.LogCameraMessage("RGB info", String.Format("Dimensions = {0}, {1} x {2} x {3}", PentaxKPImage.RGB.Rank, PentaxKPImage.RGB.GetLength(0), PentaxKPImage.RGB.GetLength(1), PentaxKPImage.RGB.GetLength(2)));

                                result = Resize(PentaxKPImage.RGB, PentaxKPImage.RGB.Rank, StartX, StartY, requestedWidth, requestedHeight);
                            }
                            else
                            {
                                DriverCommon.LogCameraMessage("RGB info as MONO", String.Format("Dimensions = {0}, {1} x {2}", PentaxKPImage.BAYER.Rank, PentaxKPImage.BAYER.GetLength(0), PentaxKPImage.BAYER.GetLength(1)));

                                result = Resize(PentaxKPImage.BAYER, PentaxKPImage.BAYER.Rank, StartX, StartY, requestedWidth, requestedHeight);
                            }
                            break;

                        default:
                             DriverCommon.LogCameraMessage("Unknown info", String.Format("{0} - Throwing", image.m_info.ImageMode));

                            throw new ASCOM.InvalidOperationException("Call to ImageArray resulted in invalid image type!");
                    }

                    return result;*/
                }
            }
        }

        public object ImageArrayVariant
        {
            get
            {
			//Fix - need to be implemented
                //using (new SerializedAccess(this, "get_ImageArrayVariant"))
                {
                    DriverCommon.LogCameraMessage("", "get_ImageArrayVariant");
                    throw new ASCOM.PropertyNotImplementedException("ImageArrayVariant", false);

/*                    PentaxKPImage image = DriverCommon.Camera.LastImage;
                    int x = 0;
                    int y = 0;
                    int c;

                    switch (image.m_info.ImageMode)
                    {
                        case 1:     // RGGB
                            int[,] rggbInput = (int[,])ImageData();
                            x = rggbInput.GetLength(0);
                            y = rggbInput.GetLength(1);
                            object[,] rggbOutput = new object[x, y];

                            for (int xcopy = 0; xcopy < x; xcopy++)
                            {
                                for (int ycopy = 0; ycopy < y; ycopy++)
                                {
                                    rggbOutput[xcopy, ycopy] = rggbInput[xcopy, ycopy];
                                }
                            }

                            return rggbOutput;


                        case 2:     // RGB
                            int[,,] rgbInput = (int[,,])ImageData();
                            x = rgbInput.GetLength(0);
                            y = rgbInput.GetLength(1);
                            c = rgbInput.GetLength(2);
                            object[,,] rgbOutput = new object[x, y, c];

                            for (int xcopy = 0; xcopy < x; xcopy++)
                            {
                                for (int ycopy = 0; ycopy < y; ycopy++)
                                {
                                    for (int ccopy = 0; ccopy < c; ccopy++)
                                    {
                                        rgbOutput[xcopy, ycopy, ccopy] = rgbInput[xcopy, ycopy, ccopy];
                                    }
                                }
                            }

                            return rgbOutput;

                        default:
                            throw new ASCOM.InvalidOperationException("Unable to detect picture format");
                    }
*/
                }
            }
        }

        public bool ImageReady
        {
            get
            {
                using (new DriverCommon.SerializedAccess("get_ImageReady", true))
                //Fix not thread safe
                {
                    DriverCommon.LogCameraMessage("", "get_ImageReady");
                    if(imagesToProcess.Count!=0)
                        return true;
                    if (bitmapsToProcess.Count != 0)
                        return true;

                    return false;                    
				}
            }
        }

        public bool IsPulseGuiding
        {
            get
            {
                //using (new SerializedAccess(this, "get_IsPulseGuiding"))
                {
                    DriverCommon.LogCameraMessage("", "get_IsPulseGuiding");
                    throw new ASCOM.PropertyNotImplementedException("IsPulseGuiding", false);
				}
            }
        }

        public double LastExposureDuration
        {
            get
            {
                //using (new SerializedAccess(this, "get_LastExposureDuration"))
                {
                    DriverCommon.LogCameraMessage("", "get_LastExposureDuration");
                    throw new ASCOM.InvalidOperationException("Call to LastExposureDuration before the first image has been taken!");
/*                using (new SerializedAccess(this, "get_LastExposureDuration", true))
                {
                    if (DriverCommon.Camera.LastImage.Status != PentaxKPImage.ImageStatus.Ready)
                    {
                        DriverCommon.LogCameraMessage("LastExposureDuration Get", "Throwing InvalidOperationException because of a call to LastExposureDuration before the first image has been taken!");
                        throw new ASCOM.InvalidOperationException("Call to LastExposureDuration before the first image has been taken!");
                    }

                    double result = DriverCommon.Camera.LastImage.Duration;
                    DriverCommon.LogCameraMessage("LastExposureDuration Get", result.ToString());

                    return result;
                }*/
				}
            }
        }

        public string LastExposureStartTime
        {
            get
            {
                //using (new SerializedAccess(this, "get_LastExposureStartTime"))
                {
                    DriverCommon.LogCameraMessage("", "get_LastExposureStartTime");
                    throw new ASCOM.InvalidOperationException("Call to LastExposureStartTime before the first image has been taken!");
/*                if (DriverCommon.Camera.LastImage.Status != PentaxKPImage.ImageStatus.Ready)
                    {
                        DriverCommon.LogCameraMessage("LastExposureStartTime Get", "Throwing InvalidOperationException because of a call to LastExposureStartTime before the first image has been taken!");
                        throw new ASCOM.InvalidOperationException("Call to LastExposureStartTime before the first image has been taken!");
                    }

                    string exposureStartString = DriverCommon.Camera.LastImage.StartTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss");
                    DriverCommon.LogCameraMessage("LastExposureStartTime Get", exposureStartString.ToString());
                    return exposureStartString;
                }*/
				}
            }
        }

        public int MaxADU
        {
            get
            {
                //using (new SerializedAccess(this, "get_MaxADU"))
                {
                    DriverCommon.LogCameraMessage("", "get_MaxADU");
                    int bpp = 8;
                    if (m_rawmode)
                        bpp = 14;
                    int maxADU = (1 << bpp) - 1;

                    return maxADU;
				}
            }
        }

        public short MaxBinX
        {
            get
            {
               //using (new SerializedAccess(this, "get_MaxBinX"))
                {
                    DriverCommon.LogCameraMessage("", "get_MaxBinX");
                    return 1;
				}
            }
        }

        public short MaxBinY
        {
            get
            {
                //using (new SerializedAccess(this, "get_MaxBinY"))
                {
                    DriverCommon.LogCameraMessage("", "get_MaxBinY");
                    return 1;
				}
            }
        }

        public int NumX
        {
            get
            {
                //using (new SerializedAccess(this, "get_NumX"))
                {
                    DriverCommon.LogCameraMessage("", "get_NumX");
                    return RequestedWidth;
				}
            }
            set
            {
                //using (new SerializedAccess(this, "set_NumX"))
                {
                    DriverCommon.LogCameraMessage("", "set_NumX");
                    RequestedWidth = value;
				}
            }
        }

        public int NumY
        {
            get
            {
                //using (new SerializedAccess(this, "get_NumY"))
                {
                    DriverCommon.LogCameraMessage("", "get_NumY");
                    return RequestedHeight;
                }
            }
            set
            {
                //using (new SerializedAccess(this, "set_NumY"))
                {
                    DriverCommon.LogCameraMessage("", "set_NumY");
                    RequestedHeight = value;
				}
            }
        }

        public short PercentCompleted
        {
            get
            {
               //using (new SerializedAccess(this, "get_PercentCompleted"))
                {
                    DriverCommon.LogCameraMessage("", "get_PercentCompleted");
                    throw new ASCOM.PropertyNotImplementedException("PercentCompleted", false);
				}
            }
        }

        public double PixelSizeX
        {
            get
            {
                 //using (new SerializedAccess(this, "get_PixelSizeX"))
                {
                    DriverCommon.LogCameraMessage("", "get_PixelSizeX");
					//Fix
                    return 0.004;// DriverCommon.Camera.Info.PixelWidth;
				}
            }
        }

        public double PixelSizeY
        {
            get
            {
                //using (new SerializedAccess(this, "get_PixelSizeY"))
                {
                    DriverCommon.LogCameraMessage("", "get_PixelSizeY");
					//Fix
                    return 0.004;// DriverCommon.Camera.Info.PixelHeight;
				}
            }
        }

        public void PulseGuide(GuideDirections Direction, int Duration)
        {
            //using (new SerializedAccess(this, "PulseGuide()"))
            {
                DriverCommon.LogCameraMessage("", "PulseGuide()");
                throw new ASCOM.MethodNotImplementedException("PulseGuide");
            }
        }

        public short ReadoutMode
        {
            get
            {
                //using (new SerializedAccess(this, "get_ReadoutMode"))
                {
                    DriverCommon.LogCameraMessage("", "get_ReadoutMode");
                    return (short)m_readoutmode;//DriverCommon.Camera.OutputMode - 1);
				}
            }
            set
            {
                //using (new SerializedAccess(this, "set_ReadoutMode"))
                {
                    DriverCommon.LogCameraMessage("", "ReadoutMode Set "+value.ToString());
                    if (ReadoutModes.Count > value)
                    {
                        switch (value)
                        {
                            case 0:
                                FastReadout = false;
                                m_readoutmode = 0;
                                NumX = 6016;
                                NumY = 4000;
                                DefaultImageWidthPixels = 6016;
                                DefaultImageHeightPixels = 4000;
                                break;

                            case 1:
                                m_readoutmode = 1;
                                NumX = 720;
                                NumY = 480;
                                DefaultImageHeightPixels = 480;
                                DefaultImageWidthPixels = 720;
                                FastReadout = true;
                                break;
                        }
                    }
                    else
                    {
                        throw new ASCOM.InvalidValueException("ReadoutMode not in allowable values");
                    }
				}
            }
        }

        public ArrayList ReadoutModes
        {
            get
            {
                //using (new SerializedAccess(this, "get_ReadoutModes"))
                {
                    DriverCommon.LogCameraMessage("","get_ReadoutModes");

                    ArrayList modes = new ArrayList();

                    modes.Add(String.Format("Full Resolution ({0} x {1})", 6016, 4000));

                                        if (true/*DriverCommon.Camera.HasLiveView*/)
                    {
                        //                        if (DriverCommon.Settings.Personality == PentaxKPCommon.PERSONALITY_NINA)
                        //                        {
                        //                            modes.Add(String.Format("LiveView ({0} x {1}) [Mono]", DriverCommon.Camera.Resolutions.PreviewWidthPixels, DriverCommon.Camera.Resolutions.PreviewHeightPixels));
                        //                        }
                        //                        else
                        //                        {
                                                    modes.Add(String.Format("LiveView ({0} x {1})", 720, 480));
                        //                        }
                    }

                    return modes;
				}
            }
        }

        public string SensorName
        {
            get
            {
                using (new DriverCommon.SerializedAccess("get_SensorName"))
                {
                    DriverCommon.LogCameraMessage("", "get_SensorName");
                    return "IMX193";// "QHY247C";// "IMX271";
				}
            }
        }

        public SensorType SensorType
        {
            get
            {
                //using (new SerializedAccess(this, "get_SensorType"))
                {
                    DriverCommon.LogCameraMessage("", "get_SensorType");
    //                type = camera.OutputMode == SonyCamera.ImageMode.RGB ? SensorType.Color : SensorType.RGGB;
                    return SensorType.Color;
				}
            }
        }

        public double SetCCDTemperature
        {
            get
            {
                //using (new SerializedAccess(this, "get_SetCCDTemperature"))
                {
                    DriverCommon.LogCameraMessage("", "get_SetCCDTemperature");
                    throw new ASCOM.PropertyNotImplementedException("SetCCDTemperature", false);
				}
            }
            set
            {
               //using (new SerializedAccess(this, "set_SetCCDTemperature"))
                {
                    DriverCommon.LogCameraMessage("", "set_SetCCDTemperature");
                    throw new ASCOM.PropertyNotImplementedException("SetCCDTemperature", true);
				}
            }
        }

        ManualResetEvent _requestTermination = new ManualResetEvent(false);

        private void StopThreadCapture()
        {
            DriverCommon.LogCameraMessage("", "StopThreadCapture");

            if (cameraThread == null || !cameraThread.IsAlive)
                return;

            _requestTermination.Set();
            cameraThread.Join();
            DriverCommon.m_camera.StopCapture();

            m_captureState = Ricoh.CameraController.CaptureState.Unknown;
        }

    private void StartThreadCapture()
        {
            _requestTermination.Reset();

            cameraThread = new Thread(() =>
            {
                using (new DriverCommon.SerializedAccess("long running task", false))
                {
                    m_captureState = Ricoh.CameraController.CaptureState.Executing;

                    bool sleepReturn = false;
                    Response response =DriverCommon.m_camera.StartCapture(false);

                    //Console.WriteLine(" result: " + response.Result.ToString() + ((response.Result == Result.Error) ? ", Code: " + response.Errors.First().Code.ToString() + ", Message : " + response.Errors.First().Message : ""));
                    m_captureState = Ricoh.CameraController.CaptureState.Unknown;

                    while (DriverCommon.m_camera.Status.CurrentCapture == null)
                       sleepReturn = _requestTermination.WaitOne(250);

                    if (!sleepReturn)
                    {
                        m_captureState = Ricoh.CameraController.CaptureState.Executing;

                        while (DriverCommon.m_camera.Status.CurrentCapture.State != Ricoh.CameraController.CaptureState.Complete)
                        {
                            DriverCommon.LogCameraMessage("long running task", DriverCommon.m_camera.Status.CurrentCapture.State.ToString() + " " + Ricoh.CameraController.CaptureState.Complete.ToString());
                            if (_requestTermination.WaitOne(250))
                                break;
                        }
                        m_captureState = DriverCommon.m_camera.Status.CurrentCapture.State;
                    }
                }

                DriverCommon.LogCameraMessage("long running task", "exiting"+m_captureState.ToString());
            });

            cameraThread.SetApartmentState(ApartmentState.MTA);
            cameraThread.Priority = ThreadPriority.Lowest;
            cameraThread.Start();
        }
 
        public void StartExposure(double Duration, bool Light)
        {
            // Light or dark frame
            // Fix it!!!!!
           if(LastSetFastReadout)
            {
                //No need to start exposure
                DriverCommon.LogCameraMessage("", "StartExposure() fast");
                m_captureState = Ricoh.CameraController.CaptureState.Executing;
                return;
            }
            using (new DriverCommon.SerializedAccess("StartExposure()"))
            {
                DriverCommon.LogCameraMessage("", "StartExposure()");

                if (Duration < 0.0)
                {
                    throw new InvalidValueException("StartExposure", "Duration", ">= 0");
                }

                ShutterSpeed shutterSpeed;
                shutterSpeed = ShutterSpeed.SS1_24000;
                if (Duration > 1.0/20000.0-0.000001)
                    shutterSpeed = ShutterSpeed.SS1_20000;
                if (Duration > 1.0 / 16000.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_16000;
                if (Duration > 1.0 / 12800.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_12800;
                if (Duration > 1.0 / 12000.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_12000;
                if (Duration > 1.0 / 10000.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_10000;
                if (Duration > 1.0 / 8000.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_8000;
                if (Duration > 1.0 / 6400.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_6400;
                if (Duration > 1.0 / 6000.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_6000;
                if (Duration > 1.0 / 5000.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_5000;
                if (Duration > 1.0 / 4000.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_4000;
                if (Duration > 1.0 / 3200.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_3200;
                if (Duration > 1.0 / 3000.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_3000;
                if (Duration > 1.0 / 2500.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_2500;
                if (Duration > 1.0 / 2000.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_2000;
                if (Duration > 1.0 / 1600.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_1600;
                if (Duration > 1.0 / 1500.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_1500;
                if (Duration > 1.0 / 1250.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_1250;
                if (Duration > 1.0 / 1000.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_1000;
                if (Duration > 1.0 / 800.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_800;
                if (Duration > 1.0 / 750.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_750;
                if (Duration > 1.0 / 640.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_640;
                if (Duration > 1.0 / 500.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_500;
                if (Duration > 1.0 / 400.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_400;
                if (Duration > 1.0 / 350.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_350;
                if (Duration > 1.0 / 320.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_320;
                if (Duration > 1.0 / 250.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_250;
                if (Duration > 1.0 / 200.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_200;
                if (Duration > 1.0 / 180.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_180;
                if (Duration > 1.0 / 160.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_160;
                if (Duration > 1.0 / 125.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_125;
                if (Duration > 1.0 / 100.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_100;
                if (Duration > 1.0 / 90.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_90;
                if (Duration > 1.0 / 80.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_80;
                if (Duration > 1.0 / 60.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_60;
                if (Duration > 1.0 / 50.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_50;
                if (Duration > 1.0 / 45.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_45;
                if (Duration > 1.0 / 40.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_40;
                if (Duration > 1.0 / 30.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_30;
                if (Duration > 1.0 / 25.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_25;
                if (Duration > 1.0 / 20.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_20;
                if (Duration > 1.0 / 15.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_15;
                if (Duration > 1.0 / 13.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_13;
                if (Duration > 1.0 / 10.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_10;
                if (Duration > 1.0 / 8.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_8;
                if (Duration > 1.0 / 6.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_6;
                if (Duration > 1.0 / 5.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_5;
                if (Duration > 1.0 / 4.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_4;
                if (Duration > 1.0 / 3.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_3;
                if (Duration > 1.0 / 2.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS1_2;
                if (Duration > 6.0 / 10.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS6_10;
                if (Duration > 7.0 / 10.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS7_10;
                if (Duration > 8.0 / 10.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS8_10;
                if (Duration > 0.99)
                    shutterSpeed = ShutterSpeed.SS1;
                if (Duration > 13.0 / 10.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS13_10;
                if (Duration > 15.0 / 10.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS15_10;
                if (Duration > 16.0 / 10.0 - 0.000001)
                    shutterSpeed = ShutterSpeed.SS16_10;
                //public static readonly ShutterSpeed SS10_13;
                //public static readonly ShutterSpeed SS10_16;
                //public static readonly ShutterSpeed SS10_25;
                //public static readonly ShutterSpeed SS25_10;
                //public static readonly ShutterSpeed SS32_10;
                //public static readonly ShutterSpeed SS3_10;
                //public static readonly ShutterSpeed SS4_10;
                //public static readonly ShutterSpeed SS5_10;
                if (Duration > 1.99)
                    shutterSpeed = ShutterSpeed.SS2;
                if (Duration > 2.99)
                    shutterSpeed = ShutterSpeed.SS3;
                if (Duration > 3.99)
                    shutterSpeed = ShutterSpeed.SS4;
                if (Duration > 4.99)
                    shutterSpeed = ShutterSpeed.SS5;
                if (Duration > 5.99)
                    shutterSpeed = ShutterSpeed.SS6;
                if (Duration > 7.99)
                    shutterSpeed = ShutterSpeed.SS8;
                if (Duration>9.99)
                       shutterSpeed=ShutterSpeed.SS10;
                if (Duration > 12.99)
                    shutterSpeed = ShutterSpeed.SS13;
                if (Duration > 14.99)
                    shutterSpeed = ShutterSpeed.SS15;
                if (Duration > 19.99)
                    shutterSpeed = ShutterSpeed.SS20;
                if (Duration > 24.99)
                    shutterSpeed = ShutterSpeed.SS25;
                if (Duration > 29.99)
                    shutterSpeed = ShutterSpeed.SS30;
                if (Duration > 39.99)
                    shutterSpeed = ShutterSpeed.SS40;
                if (Duration > 49.99)
                    shutterSpeed = ShutterSpeed.SS50;
                if (Duration > 59.99)
                    shutterSpeed = ShutterSpeed.SS60;
                if (Duration > 69.99)
                    shutterSpeed = ShutterSpeed.SS70;
                if (Duration > 79.99)
                    shutterSpeed = ShutterSpeed.SS80;
                if (Duration > 89.99)
                    shutterSpeed = ShutterSpeed.SS90;
                if (Duration > 99.99)
                    shutterSpeed = ShutterSpeed.SS100;
                if (Duration > 109.99)
                    shutterSpeed = ShutterSpeed.SS110;
                if (Duration > 119.99)
                    shutterSpeed = ShutterSpeed.SS120;
                if (Duration > 129.99)
                    shutterSpeed = ShutterSpeed.SS130;
                if (Duration > 139.99)
                    shutterSpeed = ShutterSpeed.SS140;
                if (Duration > 149.99)
                    shutterSpeed = ShutterSpeed.SS150;
                if (Duration > 159.99)
                    shutterSpeed = ShutterSpeed.SS160;
                if (Duration > 169.99)
                    shutterSpeed = ShutterSpeed.SS170;
                if (Duration > 179.99)
                    shutterSpeed = ShutterSpeed.SS180;
                if (Duration > 189.99)
                    shutterSpeed = ShutterSpeed.SS190;
                if (Duration > 199.99)
                    shutterSpeed = ShutterSpeed.SS200;
                if (Duration > 209.99)
                    shutterSpeed = ShutterSpeed.SS210;
                if (Duration > 219.99)
                    shutterSpeed = ShutterSpeed.SS220;
                if (Duration > 229.99)
                    shutterSpeed = ShutterSpeed.SS230;
                if (Duration > 239.99)
                    shutterSpeed = ShutterSpeed.SS240;
                if (Duration > 249.99)
                    shutterSpeed = ShutterSpeed.SS250;
                if (Duration > 259.99)
                    shutterSpeed = ShutterSpeed.SS260;
                if (Duration > 269.99)
                    shutterSpeed = ShutterSpeed.SS270;
                if (Duration > 279.99)
                    shutterSpeed = ShutterSpeed.SS280;
                if (Duration > 289.99)
                    shutterSpeed = ShutterSpeed.SS290;
                if (Duration > 299.99)
                    shutterSpeed = ShutterSpeed.SS300;
                if (Duration > 359.99)
                    shutterSpeed = ShutterSpeed.SS360;
                if (Duration > 419.99)
                    shutterSpeed = ShutterSpeed.SS420;
                if (Duration > 479.99)
                    shutterSpeed = ShutterSpeed.SS480;
                if (Duration > 539.99)
                    shutterSpeed = ShutterSpeed.SS540;
                if (Duration > 599.99)
                    shutterSpeed = ShutterSpeed.SS600;
                if (Duration > 659.99)
                    shutterSpeed = ShutterSpeed.SS660;
                if (Duration > 719.99)
                    shutterSpeed = ShutterSpeed.SS720;
                if (Duration > 779.99)
                    shutterSpeed = ShutterSpeed.SS780;
                if (Duration > 839.99)
                    shutterSpeed = ShutterSpeed.SS840;
                if (Duration > 899.99)
                    shutterSpeed = ShutterSpeed.SS900;
                if (Duration > 959.99)
                    shutterSpeed = ShutterSpeed.SS960;
                if (Duration > 1019.99)
                    shutterSpeed = ShutterSpeed.SS1020;
                if (Duration > 1079.99)
                    shutterSpeed = ShutterSpeed.SS1080;
                if (Duration > 1139.99)
                    shutterSpeed = ShutterSpeed.SS1140;
                if (Duration > 1199.99)
                    shutterSpeed = ShutterSpeed.SS1200;

        
                DriverCommon.m_camera.SetCaptureSettings(new List<CaptureSetting>() { shutterSpeed });

                /*if (StartX + NumX > DriverCommon.Camera.Mode.ImageWidthPixels)
                {
                    throw new InvalidValueException("StartExposure", "StartX+NumX", $"<={DriverCommon.Camera.Info.ImageWidthPixels}");
                }

                if (StartY + NumY > DriverCommon.Camera.Mode.ImageHeightPixels)
                {
                    throw new InvalidValueException("StartExposure", "StartX+NumX", $"<={DriverCommon.Camera.Info.ImageHeightPixels}");
                }

                if (!LastSetFastReadout)
                {
                    if (Duration <= 1.0e-5 && DriverCommon.Camera.HasLiveView && DriverCommon.Settings.AutoLiveview)
                    {
                        //                        DriverCommon.Camera.PreviewMode = true;
                    }
                    else
                    {
                        //                        DriverCommon.Camera.PreviewMode = false;
                    }
                }*/
                //if (cameraThread==null||!cameraThread.IsAlive||m_captureState == Ricoh.CameraController.CaptureState.Complete)
                {
                    //    if (cameraThread != null && cameraThread.IsAlive)
                    //        cameraThread.Abort();
                    //if(m_captureState == Ricoh.CameraController.CaptureState.Executing)
                    //    throw new ASCOM.InvalidOperationException("Camera already capturing");
                    //else
                    StartThreadCapture();
                }
                /*else
                {
                    throw new ASCOM.PropertyNotImplementedException("SetCCDTemperature", true);
                }*/
            }
        }

    public int StartX
    {
        get
        {
                //using (new SerializedAccess(this, "get_StartX"))
                {
//                    DriverCommon.LogCameraMessage("StartX Get", RequestedStartX.ToString());
                    DriverCommon.LogCameraMessage("", "get_StartX");
                    return RequestedStartX;
				}
        }
        set
        {
               //using (new SerializedAccess(this, "set_StartX"))
                {
                    DriverCommon.LogCameraMessage("", "set_StartY");
                    RequestedStartX = value;
				}
        }
    }

    public int StartY
    {
        get
        {
               //using (new SerializedAccess(this, "get_StartY"))
                {
                    DriverCommon.LogCameraMessage("", "get_StartY");
                    return RequestedStartY;
				}
        }
        set
        {
                //using (new SerializedAccess(this, "set_StartY"))
                {
                    DriverCommon.LogCameraMessage("", "set_StartY");
                    RequestedStartY = value;
				}
        }
    }

        public void StopExposure()
        {
            DriverCommon.LogCameraMessage("", "StopExposure");

            StopThreadCapture();

            if (LastSetFastReadout)
            {
                //No need to start exposure
                DriverCommon.LogCameraMessage("", "StopExposure() fast");
                m_captureState = Ricoh.CameraController.CaptureState.Executing;
            }

        }

        #endregion

        #region Private properties and methods
        // here are some useful properties and methods that can be used as required
        // to help with driver development

        #region ASCOM Registration

        // Register or unregister driver for ASCOM. This is harmless if already
        // registered or unregistered. 
        //
        /// <summary>
        /// Register or unregister the driver with the ASCOM Platform.
        /// This is harmless if the driver is already registered/unregistered.
        /// </summary>
        /// <param name="bRegister">If <c>true</c>, registers the driver, otherwise unregisters it.</param>
        private static void RegUnregASCOM(bool bRegister)
        {
            using (var P = new ASCOM.Utilities.Profile())
            {
                P.DeviceType = "Camera";
                if (bRegister)
                {
                    P.Register(DriverCommon.CameraDriverId, DriverCommon.CameraDriverDescription);
                }
                else
                {
                    P.Unregister(DriverCommon.CameraDriverId);
                }
            }
        }

        /// <summary>
        /// This function registers the driver with the ASCOM Chooser and
        /// is called automatically whenever this class is registered for COM Interop.
        /// </summary>
        /// <param name="t">Type of the class being registered, not used.</param>
        /// <remarks>
        /// This method typically runs in two distinct situations:
        /// <list type="numbered">
        /// <item>
        /// In Visual Studio, when the project is successfully built.
        /// For this to work correctly, the option <c>Register for COM Interop</c>
        /// must be enabled in the project settings.
        /// </item>
        /// <item>During setup, when the installer registers the assembly for COM Interop.</item>
        /// </list>
        /// This technique should mean that it is never necessary to manually register a driver with ASCOM.
        /// </remarks>
        [ComRegisterFunction]
        public static void RegisterASCOM(Type t)
        {
            RegUnregASCOM(true);
        }

        /// <summary>
        /// This function unregisters the driver from the ASCOM Chooser and
        /// is called automatically whenever this class is unregistered from COM Interop.
        /// </summary>
        /// <param name="t">Type of the class being registered, not used.</param>
        /// <remarks>
        /// This method typically runs in two distinct situations:
        /// <list type="numbered">
        /// <item>
        /// In Visual Studio, when the project is cleaned or prior to rebuilding.
        /// For this to work correctly, the option <c>Register for COM Interop</c>
        /// must be enabled in the project settings.
        /// </item>
        /// <item>During uninstall, when the installer unregisters the assembly from COM Interop.</item>
        /// </list>
        /// This technique should mean that it is never necessary to manually unregister a driver from ASCOM.
        /// </remarks>
        [ComUnregisterFunction]
        public static void UnregisterASCOM(Type t)
        {
            RegUnregASCOM(false);
        }

        #endregion

        /// <summary>
        /// Returns true if there is a valid connection to the driver hardware
        /// </summary>
        private bool IsConnected
        {
            get
            {
                using (new DriverCommon.SerializedAccess("IsConnected"))
                {
                    return DriverCommon.m_camera.IsConnected(Ricoh.CameraController.DeviceInterface.USB);
                }
            }
        }

        /// <summary>
        /// Use this function to throw an exception if we aren't connected to the hardware
        /// </summary>
        /// <param name="message"></param>
        private void CheckConnected(string message)
        {
            if (!IsConnected)
            {
                DriverCommon.LogCameraMessage("CheckConnected", message);
                throw new ASCOM.NotConnectedException(message);
            }
        }

        #endregion

        internal static object Resize(object array, int rank, int startX, int startY, int width, int height)
        {
            DriverCommon.LogCameraMessage("Resize", string.Format("rank={0}, startX={1}, startY={2}, width={3}, height={4}", rank, startX, startY, width, height));

            if (rank == 2)
            {
                int[,] input = (int[,])array;

                if (startX == 0 && startY == 0 && width >= input.GetLength(0) && height >= input.GetLength(1))
                {
                    return input;
                }

                int[,] output = new int[width, height];

                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        output[x, y] = input[x + startX, y + startY];
                    }
                }

                return output;
            }
            else if (rank == 3)
            {
                int[,,] input = (int[,,])array;

                if (startX == 0 && startY == 0 && width >= input.GetLength(0) && height >= input.GetLength(1))
                {
                    DriverCommon.LogCameraMessage("Resize","returning original values");
                    return input;
                }

                int zLen = input.GetLength(2);
                int[,,] output = new int[width, height, zLen];

                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        for (int z = 0; z < zLen; z++)
                        {
                            output[x, y, z] = input[x + startX, y + startY, z];
                        }
                    }
                }

                return output;
            }
            else
            {
                // Ummm
                throw new ASCOM.InvalidValueException();
            }
        }

     }
}
