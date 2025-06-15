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
        internal short gainIndex=0;
        internal ArrayList m_gains;
        internal static int m_readoutmode=0;

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
            m_gains.Add("ISO200");

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
                                    List<CaptureSetting> swsettings = sw.AvailableSettings;

                                    foreach (CaptureSetting setting in swsettings)
                                    {
                                        DriverCommon.LogCameraMessage(setting.Name,setting.Value.ToString());
                                    }
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
                //using (new SerializedAccess(this, "get_ExposureMin", true))
                {
                    DriverCommon.LogCameraMessage("", "get_ExposureMin");
                    return 0.001;
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
                    return 0.001;
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
                    // Fix if connected
                    if (Connected)
                    {
                        return gainIndex;
/*                        if (DriverCommon.Settings.AllowISOAdjust && DriverCommon.Camera.Gains.Count > 0)
                        {
                            short gainIndex = DriverCommon.Camera.GainIndex;
                            DriverCommon.LogCameraMessage("Gain Get", gainIndex.ToString());

                            return gainIndex;
                        }
                        else
                        {
                            throw new ASCOM.PropertyNotImplementedException("Gains property is not enabled, see driver settings dialog");
                        }*/
                    }
                    else
                    {
                        throw new ASCOM.NotConnectedException("Camera must be connected to retrieve gain");
                    }
				}
            }

            set
            {
               using (new DriverCommon.SerializedAccess("set_Gain"))
                {
                    DriverCommon.LogCameraMessage("", "set_Gain");
                    if (Connected)
                    {
                        gainIndex = value;
                        /*if (DriverCommon.Settings.AllowISOAdjust && DriverCommon.Camera.Gains.Count > 0)
                        {
                            DriverCommon.Camera.GainIndex = value;
                            DriverCommon.LogCameraMessage("Gain Set", value.ToString());
                        }
                        else
                        {
                            throw new ASCOM.PropertyNotImplementedException("Gains property is not enabled, see driver settings dialog");
                        }*/
                    }
                    else
                    {
                        throw new ASCOM.NotConnectedException("Camera must be connected to retrieve gain");
                    }
				}
            }
        }

        public short GainMax
        {
            get
            {
                using (new DriverCommon.SerializedAccess("get_GainMax"))
                {
                    DriverCommon.LogCameraMessage("", "get_GainMax");
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
                    throw new ASCOM.PropertyNotImplementedException("GainMin", true);
				}
            }
        }

        public ArrayList Gains
        {
            get
            {
               using (new DriverCommon.SerializedAccess("get_Gains"))
               {
                    DriverCommon.LogCameraMessage("", "get_Gains");
                    if (Connected)
                    {
                        return m_gains;
/*                        ArrayList gains = DriverCommon.Camera.Gains;

                        if (DriverCommon.Settings.AllowISOAdjust && gains.Count > 0)
                        {

                            DriverCommon.LogCameraMessage("Gains Get", String.Format("Size = {0}", gains.Count));

                            return gains;
                        }
                        else
                        {
                            throw new ASCOM.PropertyNotImplementedException("Gains property is not enabled, see driver settings dialog");
                        }*/
                    }
                    else
                    {
                        throw new ASCOM.NotConnectedException("Camera must be connected to get list of available gains");
                    }
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
                        DriverCommon.LogCameraMessage("", "Calling ReadImageFileQuick");
                        result=ReadImageFileQuick(imageName);
                        return result;
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
                    int bpp = 8;//(int)DriverCommon.Camera.Info.BitsPerPixel;
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
                    {
                        DriverCommon.m_camera.StartCapture(false);
                    }

                    m_captureState = Ricoh.CameraController.CaptureState.Unknown;

                    bool sleepReturn=false;
                    while (DriverCommon.m_camera.Status.CurrentCapture == null)
                        sleepReturn=_requestTermination.WaitOne(250);

                    //                    int i = 0;
                    if (!sleepReturn)
                    {
                        m_captureState = Ricoh.CameraController.CaptureState.Executing;

                        while (DriverCommon.m_camera.Status.CurrentCapture.State != Ricoh.CameraController.CaptureState.Complete)
                        {
                            //                        i++;
                            DriverCommon.LogCameraMessage("long running task", DriverCommon.m_camera.Status.CurrentCapture.State.ToString() + " " + Ricoh.CameraController.CaptureState.Complete.ToString());
                            if (_requestTermination.WaitOne(250))
                                //                        if (i > 100)
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

                /*if (Duration < 0.0)
                {
                    throw new InvalidValueException("StartExposure", "Duration", ">= 0");
                }

                if (StartX + NumX > DriverCommon.Camera.Mode.ImageWidthPixels)
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
