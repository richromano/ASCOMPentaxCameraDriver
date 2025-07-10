//tabs=4
// --------------------------------------------------------------------------------
//
// ASCOM Camera driver for Pentax KP Camera
//
// Description:	Implements ASCOM driver for Pentax KP camera.
//				Communicates using USB connection.
//
// Implements:	ASCOM Camera interface version: 2
// Author:		(2025) Richard Romano
//
#define Camera

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using ASCOM.Astrometry.AstroUtils;
using ASCOM.Utilities;
using ASCOM.DeviceInterface;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using Ricoh.CameraController;
using System.Windows.Media.Imaging;
using System.Drawing;
using System.Drawing.Imaging;
using ASCOM.PentaxKP.Classes;

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
    public class Camera : ICameraV4
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
        internal static int RequestedWidth = 0;//6016; // In NumX and NumY
        internal static int RequestedHeight = 0;//4000;
        internal static int MaxImageWidthPixels = 0;//6016; // Constants to define the ccd pixel dimenstion
        internal static int MaxImageHeightPixels = 0;//4000;
        internal static Queue<String> imagesToProcess = new Queue<string>();
        internal static Queue<BitmapImage> bitmapsToProcess = new Queue<BitmapImage>();
        private ImageDataProcessor _imageDataProcessor;
        // Index to the current ISO level
        internal short gainIndex;
        // The different ISO levels
        internal ArrayList m_gains;

        // Two output modes 0=6016x4000 standard and 1=720x480 liveview
        internal static int m_readoutmode=0;
        internal static double previousDuration=0;
        internal static string lastCaptureResponse="None";
        internal static string canceledCaptureResponse="None";
        internal static DateTime lastCaptureStartTime = DateTime.MinValue;

        // TODO:  - all these statics means there can only be one camera

        //        internal static Ricoh.CameraController.CaptureState m_captureState = Ricoh.CameraController.CaptureState.Unknown;
        internal static CameraStates m_captureState = CameraStates.cameraError;

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
                if (LastSetFastReadout&&m_captureState == CameraStates.cameraExposing) {
                    bitmapsToProcess.Enqueue(bitmapImage);
                    m_captureState = CameraStates.cameraIdle;
                    DriverCommon.LogCameraMessage(1,"", "Enqueued LiveView Image");
                }

            }

            // Image Added
            public override void ImageAdded(CameraDevice sender, CameraImage image)
            {
                DriverCommon.LogCameraMessage(5, "", "Received Image " + image.Name + " Capture state "+m_captureState.ToString());
                DriverCommon.LogCameraMessage(5, "", sender.Status.CurrentCapture.ID.ToString() + " " + lastCaptureResponse.ToString()+" "+canceledCaptureResponse.ToString());

                if (lastCaptureResponse == canceledCaptureResponse)
                {
                    image.Delete();
                    return;
                }

                // Get the image and save it in the current directory
                if ((!LastSetFastReadout)&&(m_captureState==CameraStates.cameraExposing))
                    using (FileStream fs = new FileStream(
                        System.IO.Path.GetTempPath() + Path.DirectorySeparatorChar +
                        image.Name, FileMode.Create, FileAccess.Write))
                    {
                        m_captureState = CameraStates.cameraReading;
                        // TODO: Add frame progress
                        Response imageGetResponse = image.GetData(fs);
                        DriverCommon.LogCameraMessage(0,"","Get Image has " +
                            (imageGetResponse.Result == Result.OK ?
                                "SUCCEED." : "FAILED."));
                        // TODO: save to memory instead MemoryStream
                        DriverCommon.LogCameraMessage(0,"", System.IO.Path.GetTempPath() + Path.DirectorySeparatorChar +
                        image.Name);
                        imagesToProcess.Enqueue(System.IO.Path.GetTempPath() + Path.DirectorySeparatorChar + image.Name);
                    }
            }

            // Capture Complete
            public override void CaptureComplete(CameraDevice sender, Capture capture)
            {
                m_captureState = CameraStates.cameraIdle;
                DriverCommon.LogCameraMessage(0,"","Capture Complete. Capture ID: "+capture.ID.ToString()+" tracking "+lastCaptureResponse.ToString()+" "+canceledCaptureResponse.ToString());
            }

            public override void DeviceDisconnected(CameraDevice sender, Ricoh.CameraController.DeviceInterface deviceInterface)
            {
                //Best we can do
                DriverCommon.LogCameraMessage(0,"","Device Disconnected.");
                //_requestTermination.Set();
                m_captureState = CameraStates.cameraError;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PentaxKP"/> class.
        /// Must be public for COM registration.
        /// </summary>
        public Camera()
        {
            gainIndex = 0 ;
            m_gains = new ArrayList();
            m_gains.Add("ISO 100");
            m_gains.Add("ISO 200");
            m_gains.Add("ISO 400");
            m_gains.Add("ISO 800");
            m_gains.Add("ISO 1600");
            m_gains.Add("ISO 3200");

            _imageDataProcessor = new ImageDataProcessor();

            DriverCommon.ReadProfile(); // Read device configuration from the ASCOM Profile store

            DriverCommon.LogCameraMessage(0,"Camera", "Starting initialisation");

            utilities = new Util(); //Initialise util object
            astroUtilities = new AstroUtils(); // Initialise astro utilities object

            DriverCommon.LogCameraMessage(0,"Camera", "Completed initialisation");

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
            DriverCommon.LogCameraMessage(0,"SetupDialog", "[in]");
            if (IsConnected) {
                System.Windows.Forms.MessageBox.Show("Camera is currently connected.  Please disconnect and reconnect to change settings.");
                DriverCommon.LogCameraMessage(0,"SetupDialog", "[out]");
                return;
            }

            // TODO: 
            //Should we cancel capture and disconnect?

            using (SetupDialogForm F = new SetupDialogForm())
            {
                var result = F.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    DriverCommon.WriteProfile(); // Persist device configuration values to the ASCOM Profile store
                }
                else
                    // TODO: need to fix connect
                    throw new ASCOM.DriverException("User canceled");
            }

            DriverCommon.LogCameraMessage(0,"SetupDialog", "[out]");
        }

        public double SubExposureDuration
        {
            get
            {
                throw new ASCOM.ActionNotImplementedException("Action is not implemented by this driver");
            }
            set
            {
                throw new ASCOM.ActionNotImplementedException("Action is not implemented by this driver");
            }
        }

        public int Offset
        {
            get
            {
                throw new ASCOM.ActionNotImplementedException("Action is not implemented by this driver");
            }
            set
            {
                throw new ASCOM.ActionNotImplementedException("Action is not implemented by this driver");
            }
        }

        public int OffsetMax
        {
            get
            {
                throw new ASCOM.ActionNotImplementedException("Action is not implemented by this driver");
            }
            set
            {
                throw new ASCOM.ActionNotImplementedException("Action is not implemented by this driver");
            }
        }

        public int OffsetMin
        {
            get
            {
                throw new ASCOM.ActionNotImplementedException("Action is not implemented by this driver");
            }
            set
            {
                throw new ASCOM.ActionNotImplementedException("Action is not implemented by this driver");
            }
        }

        public ArrayList Offsets
        {
            get
            {
                throw new ASCOM.ActionNotImplementedException("Action is not implemented by this driver");
            }
            set
            {
                throw new ASCOM.ActionNotImplementedException("Action is not implemented by this driver");
            }
        }

        public ArrayList SupportedActions
        {
            get
            {
                DriverCommon.LogCameraMessage(0,"SupportedActions Get", "Returning empty arraylist");
                return new ArrayList();
            }
        }

        public string Action(string actionName, string actionParameters)
        {
            DriverCommon.LogCameraMessage(0,"", $"Action {actionName}, parameters {actionParameters} not implemented");
            throw new ASCOM.ActionNotImplementedException("Action " + actionName + " is not implemented by this driver");
        }

        public void CommandBlind(string command, bool raw)
        {
            DriverCommon.LogCameraMessage(0,"", $"CommandBlind {command} not implemented");
            throw new ASCOM.MethodNotImplementedException("CommandBlind");
        }

        public bool CommandBool(string command, bool raw)
        {
            DriverCommon.LogCameraMessage(0,"", $"CommandBool {command} not implemented");
            throw new ASCOM.MethodNotImplementedException("CommandBool");
        }

        public string CommandString(string command, bool raw)
        {
            DriverCommon.LogCameraMessage(0,"", $"CommandString {command} not implemented");
            throw new ASCOM.MethodNotImplementedException("CommandString");
        }

        public void Dispose()
        {
            DriverCommon.LogCameraMessage(0,"Dispose", "Disposing");
			Connected = false;
            utilities.Dispose();
            utilities = null;
            astroUtilities.Dispose();
            astroUtilities = null;
        }

        public async void Connect()
        {
            DriverCommon.LogCameraMessage(0, "Connect", "async Connect not supported");
            await Task.Run(() => {
                DriverCommon.m_camera = CameraDeviceDetector.Detect(Ricoh.CameraController.DeviceInterface.USB).FirstOrDefault();
                if (DriverCommon.m_camera != null)
                {
                    if (DriverCommon.m_camera.EventListeners.Count == 0)
                    {
                        DriverCommon.m_camera.EventListeners.Add(new EventListener());
                    }
                    var response = DriverCommon.m_camera.Connect(Ricoh.CameraController.DeviceInterface.USB);
                    if (response.Result.Equals(Response.OK))
                    {
                        DriverCommon.LogCameraMessage(0, "Connect","Connected. Model: " + DriverCommon.m_camera.Model + ", SerialNumber:" + DriverCommon.m_camera.SerialNumber);
                    }
                    else
                    {
                        DriverCommon.LogCameraMessage(0, "Connect", "Connection has failed.");
                    }
                }
                else
                {
                    DriverCommon.LogCameraMessage(0, "Connect", "Device not found.");
                }
            });
        }

        public bool Connecting
        {
            get
            {
                DriverCommon.LogCameraMessage(0, "", "Connecting");
                // TODO: this should check on the connect task completing
                if (DriverCommon.m_camera.IsConnected(Ricoh.CameraController.DeviceInterface.USB))
                    return false;

                return true;
            }
        }

        public async void Disconnect()
        {
            if (DriverCommon.m_camera == null) { return; }
            DriverCommon.LogCameraMessage(0, "Connect", "Disconnecting...");
            await Task.Run(() =>
            {
                DriverCommon.m_camera.Disconnect(Ricoh.CameraController.DeviceInterface.USB);
                DriverCommon.LogCameraMessage(0, "Connect", "Disconnected.");
                DriverCommon.m_camera = null;
                m_captureState = CameraStates.cameraError;
            });

        }

        public bool Connected
        {
            get
            {
                //using (new DriverCommon.SerializedAccess("get_Connected"))
                {
                    DriverCommon.LogCameraMessage(5,"Connected", "get");
                    if (DriverCommon.m_camera == null)
                        return false;

                    return DriverCommon.m_camera.IsConnected(Ricoh.CameraController.DeviceInterface.USB);
				}
            }
            set
            {
                // TODO: set_Connected called by Sharpcap and NINA
                DriverCommon.LogCameraMessage(0, "", $"set_Connected Set {value.ToString()}");
                //throw new ASCOM.ActionNotImplementedException("set_Connected is not implemented by this driver");
                //using (new DriverCommon.SerializedAccess("set_Connected", false))
                {
//                    DriverCommon.LogCameraMessage(0,"", $"set_Connected Set {value.ToString()}");
                    //if (value&& (DriverCommon.m_camera == null|| !DriverCommon.m_camera.IsConnected(Ricoh.CameraController.DeviceInterface.USB)))
                    if(value)
                    {
                        if (DriverCommon.m_camera == null)
                        {
	                        SetupDialog();
                            DriverCommon.LogCameraMessage(0,"Connected", "Connecting...");
                            List<CameraDevice> detectedCameraDevices = CameraDeviceDetector.Detect(Ricoh.CameraController.DeviceInterface.USB);
                            DriverCommon.m_camera = detectedCameraDevices.ElementAt(DriverCommon.Settings.DeviceIndex);
                            if (DriverCommon.m_camera != null)
                            {
                                var response = DriverCommon.m_camera.Connect(Ricoh.CameraController.DeviceInterface.USB);
                                if (response.Equals(Response.OK)/*&& (DriverCommon.m_camera.IsConnected(Ricoh.CameraController.DeviceInterface.USB))*/)
                                {
                                    DriverCommon.LogCameraMessage(0,"Connected", "Connected. Model: " + DriverCommon.m_camera.Model + ", SerialNumber:" + DriverCommon.m_camera.SerialNumber);
                                    StorageWriting sw = new StorageWriting();
                                    sw=Ricoh.CameraController.StorageWriting.False;
                                    ExposureProgram ep = new ExposureProgram();
                                    ep = Ricoh.CameraController.ExposureProgram.Bulb;
                                    StillImageCaptureFormat sicf = new StillImageCaptureFormat();

                                    sicf = Ricoh.CameraController.StillImageCaptureFormat.JPEG;
                                    if(DriverCommon.Settings.DefaultReadoutMode==PentaxKPProfile.OUTPUTFORMAT_RAWBGR
                                        || DriverCommon.Settings.DefaultReadoutMode == PentaxKPProfile.OUTPUTFORMAT_RGGB)
                                        sicf = Ricoh.CameraController.StillImageCaptureFormat.DNG;
                                    StillImageQuality siq = new StillImageQuality();
                                    siq=Ricoh.CameraController.StillImageQuality.LargeBest;
                                    //DriverCommon.m_camera.SetCaptureSettings(new List<CaptureSetting>() { ep });
                                    DriverCommon.m_camera.SetCaptureSettings(new List<CaptureSetting>() { sw });
                                    DriverCommon.m_camera.SetCaptureSettings(new List<CaptureSetting>() { siq });
                                    DriverCommon.m_camera.SetCaptureSettings(new List<CaptureSetting>() { sicf });
                                    // Sleep to let the settings take effect
                                    Thread.Sleep(1000);
                                    if (DriverCommon.Settings.UseLiveview)
                                        DriverCommon.m_camera.StartLiveView();

                                    string deviceModel = DriverCommon.Settings.DeviceId;
                                    DriverCommon.Settings.assignCamera(deviceModel);
                                    MaxImageWidthPixels = DriverCommon.Settings.Info.ImageWidthPixels; // Constants to define the ccd pixel dimension
                                    MaxImageHeightPixels = DriverCommon.Settings.Info.ImageHeightPixels;
                                    StartX = 0;
                                    StartY = 0;
                                    NumX = MaxImageWidthPixels;
                                    NumY = MaxImageHeightPixels;

                                    Gain = gainIndex;
                                    m_captureState = CameraStates.cameraIdle;
                                }
                                else
                                {
                                    DriverCommon.LogCameraMessage(0,"Connected", "Connection is failed.");
                                }



                                if (DriverCommon.m_camera.EventListeners.Count == 0)
                                    DriverCommon.m_camera.EventListeners.Add(new EventListener());
                            }
                            else
                            {
                                DriverCommon.LogCameraMessage(0,"Connected", "Device has not found.");
                            }
                        }
                    }
                    else
                    {
                        if (DriverCommon.m_camera != null)
                        {
                            // Stop the capture if necessary
                            // TODO: Should be async
                            DriverCommon.m_camera.Disconnect(Ricoh.CameraController.DeviceInterface.USB);
                        }

                        m_captureState = CameraStates.cameraError;
                        DriverCommon.m_camera = null;
                        DriverCommon.LogCameraMessage(0,"Connected", "Closed connection to camera");
                    }
                }
            }
        }

        public string Description
        {
            get
            {
                //using (new DriverCommon.SerializedAccess("get_Description"))
                {
                    DriverCommon.LogCameraMessage(0,"", "get_Description");
//                    return DriverCommon.m_camera.Model;
	                return DriverCommon.CameraDriverDescription;
                }
            }
        }

        public string DriverInfo
        {
            get
            {
                DriverCommon.LogCameraMessage(0,"", "get_DriverInfo");
                return DriverCommon.CameraDriverInfo;
            }
        }

        public string DriverVersion
        {
            get
            {
                DriverCommon.LogCameraMessage(0,"", "get_DriverVersion");
                return DriverCommon.DriverVersion;
            }
        }

        public short InterfaceVersion
        {
            get
            {
                DriverCommon.LogCameraMessage(0,"", "get_InterfaceVersion");
                return Convert.ToInt16("4");
            }
        }

        public string Name
        {
            get
            {
                DriverCommon.LogCameraMessage(0,"", "get_Name");
                return DriverCommon.CameraDriverName;
            }
        }

        #endregion

        #region ICamera Implementation

        public async void AbortExposure()
        {
            // TODO: fix abort exposure
            DriverCommon.LogCameraMessage(0, "", "AbortExposure");
            if (LastSetFastReadout)
            {
                m_captureState = CameraStates.cameraIdle;
                return;
            }

            // TODO: cameraWaiting is bad because it will get set to other
            if (m_captureState != CameraStates.cameraExposing && m_captureState != CameraStates.cameraWaiting)
                return;

            //StopCapture doesn't get called
            await Task.Run(() =>
            {
                DriverCommon.LogCameraMessage(0, "AbortExposure", "Stopping Capture.");
                while(m_captureState!=CameraStates.cameraExposing)
                {
                    Thread.Sleep(100);
                    DriverCommon.LogCameraMessage(0, "AbortExposure", "Waiting for capture to start.");
                }

                canceledCaptureResponse = lastCaptureResponse;
                /*if (previousDuration > 5)
                {
                    //DriverCommon.m_camera.Disconnect(Ricoh.CameraController.DeviceInterface.USB);
                    //m_captureState = CameraStates.cameraError;
                    Disconnect();
                    return;
                }*/
                //Response response =DriverCommon.m_camera.StopCapture();

                while(m_captureState==CameraStates.cameraExposing)
                {
                    Thread.Sleep(100);
                    DriverCommon.LogCameraMessage(0, "AbortExposure", "Waiting for capture to finish.");
                }

                return;
                //DriverCommon.LogCameraMessage(0, "AbortExposure", "Failed. "+response.Errors.First().Message);
                //return;
            });
        }

        public short BayerOffsetX
        {
            get
            {
                //using (new SerializedAccess(this, "get_BayerOffsetX"))
                {
                    DriverCommon.LogCameraMessage(0,"", "get_BayerOffsetX");
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
                    DriverCommon.LogCameraMessage(0,"", "get_BayerOffsetY");
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
                    DriverCommon.LogCameraMessage(0,"", "get_BinX");
                    return 1;
                }

            }
            set
            {
                //using (new SerializedAccess(this, "set_BinX"))
                {
                    DriverCommon.LogCameraMessage(0,"", "set_BinX");
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
                    DriverCommon.LogCameraMessage(0,"", "get_BinY");
                    return 1;
                }
            }
            set
            {
                //using (new SerializedAccess(this, "set_BinY"))
                {
                    DriverCommon.LogCameraMessage(0,"", "set_BinY");
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
                    DriverCommon.LogCameraMessage(4,"", "get_CCDTemperature");
                   double temperature = 20; // Celcius
                   return temperature;
				}
            }
        }

        public CameraStates CameraState
        {
            get
            {
                //using (new DriverCommon.SerializedAccess("get_CameraState", true))
                {
                    DriverCommon.LogCameraMessage(0,"", $"get_CameraState {m_captureState.ToString()}");
                    if(m_captureState==CameraStates.cameraReading)
                    {
                        if (DriverCommon.m_camera.Status.CurrentCapture.Equals(Ricoh.CameraController.CaptureState.Complete))
                        {
                            DriverCommon.LogCameraMessage(0, "", "Setting capture to idle");
                            m_captureState = CameraStates.cameraIdle;
                        }
                    }
                    // TODO: !!!! Look at camera state diagram
                    return m_captureState;
/*                    switch (m_captureState)
                    {
                        case Ricoh.CameraController.CaptureState.Executing:
                            return CameraStates.cameraExposing;

                        case Ricoh.CameraController.CaptureState.Complete:
                            return CameraStates.cameraIdle;

                        default:
                            return CameraStates.cameraIdle;
                    }*/
                }
            }
        }

        public int CameraXSize
        {
            get
            {
                //using (new SerializedAccess(this, "get_CameraXSize", true))
                {
                    DriverCommon.LogCameraMessage(0,"", "get_CameraXSize "+MaxImageWidthPixels.ToString());
                    return MaxImageWidthPixels;
				}
            }
        }

        public int CameraYSize
        {
            get
            {
                //using (new SerializedAccess(this, "get_CameraYSize", true))
                {
                    DriverCommon.LogCameraMessage(0,"", "get_CameraYSize "+MaxImageHeightPixels.ToString());
                    return MaxImageHeightPixels;
				}
            }
        }

        public bool CanAbortExposure
        {
            get
            {
                //using (new SerializedAccess(this, "get_CanAbortExposure"))
                {
                    DriverCommon.LogCameraMessage(0, "", "get_CanAbortExposure");
                    //                    if (LastSetFastReadout)
                    //                        return false;
                    //                    else
                    //                    if (DriverCommon.Settings.UseLiveview)
                    //                        return false;
                    //                    if (LastSetFastReadout)
                    //                        return true;

                    //                    if (m_captureState != CameraStates.cameraExposing)
                    //                        return false;

                    if (DriverCommon.Settings.Personality == PentaxKPProfile.PERSONALITY_NINA)
                        return true;

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
                    DriverCommon.LogCameraMessage(0,"", "get_CanAsymmetricBin");
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
                    DriverCommon.LogCameraMessage(0,"", "get_CanFastReadout");
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
                    DriverCommon.LogCameraMessage(0,"", "get_CanGetCoolerPower");
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
                    DriverCommon.LogCameraMessage(0,"", "get_CanPulseGuide");
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
                    DriverCommon.LogCameraMessage(0,"", "get_CanSetCCDTemperature");
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
                    DriverCommon.LogCameraMessage(0,"", "get_CanStopExposure");
                    if (LastSetFastReadout)
                        return true;

                    return false;
				}
            }
        }

        public bool CoolerOn
        {
            get
            {
                //using (new SerializedAccess(this, "get_CoolerOn"))
                {
                    DriverCommon.LogCameraMessage(0,"", "get_CoolerOn");
                    throw new ASCOM.PropertyNotImplementedException("CoolerOn", false);
				}
            }
            set
            {
                //using (new SerializedAccess(this, "set_CoolerOn"))
                {
                    DriverCommon.LogCameraMessage(0,"", "set_CoolerOn");
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
                    DriverCommon.LogCameraMessage(0,"", "get_CoolerPower");
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
                    DriverCommon.LogCameraMessage(0,"", "get_ElectronsPerADU");
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
                    DriverCommon.LogCameraMessage(0,"", "get_ExposureMax");
                    return 1200;
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
                    DriverCommon.LogCameraMessage(0,"", "get_ExposureMin");
                    return 1.0/24000.0;
				}
            }
        }

        public double ExposureResolution
        {
            get
            {
                //using (new SerializedAccess(this, "get_ExposureResolution", true))
                {
                    DriverCommon.LogCameraMessage(0,"", "get_ExposureResolution");
                    return 1.0/24000.0;
				}
            }
        }

        public bool FastReadout
        {
            // This is called by set_mode if the mode includes FastReadout
            get
            {
                //using (new SerializedAccess(this, "get_FastReadout"))
                {
                    DriverCommon.LogCameraMessage(0,"", "get_FastReadout");
                    return LastSetFastReadout;
				}
            }
            set
            {
                //using (new DriverCommon.SerializedAccess("set_FastReadout", false))
                {
                    DriverCommon.LogCameraMessage(0,"", "set_FastReadout");
                    if (m_captureState != CameraStates.cameraIdle)
                        throw new ASCOM.InvalidOperationException("Call to set_FastReadout when camera not Idle!");

                    if (LastSetFastReadout)
                    {
                        if (!value)
                        {
                            LastSetFastReadout = false;
                            // TODO: Review?
                            DriverCommon.m_camera.StopLiveView();
                            Thread.Sleep(500);
                            // Need to clear because the expected format has changed
                            bitmapsToProcess.Clear();
                            //imagesToProcess.Clear();
                            if (DriverCommon.Settings.UseLiveview)
                                DriverCommon.m_camera.StartLiveView();
                        }
                        //else
                        //In FastReadout we don't do any real captures so cancel the current one
                        //StopThreadCapture();
                    }
                    else
                    {
                        if (value)
                        {
                            DriverCommon.m_camera.StartLiveView();
                            // Need to clear because the expected format has changed
                            //StopThreadCapture();
                            imagesToProcess.Clear();
                        }
                    }
                    LastSetFastReadout = value;
				}
            }
        }

        public double FullWellCapacity
        {
            get
            {
                //using (new SerializedAccess(this, "get_FullWellCapacity"))
                {
                    DriverCommon.LogCameraMessage(0,"", "get_FullWellCapacity");
                    throw new ASCOM.PropertyNotImplementedException("FullWellCapacity", false);
				}
            }
        }

        public short Gain
        {
            get
            {
                //using (new DriverCommon.SerializedAccess("get_Gain"))
                {
                    DriverCommon.LogCameraMessage(0,"", "get_Gain");
                    return gainIndex;
				}
            }

            set
            {
               //using (new DriverCommon.SerializedAccess("set_Gain"))
                {
                    // Check connected
                    DriverCommon.LogCameraMessage(0,"", "set_Gain "+value.ToString());
                    gainIndex = value;
                    if (gainIndex < 0)
                        gainIndex = 0;
                    if (gainIndex > 5)
                        gainIndex = 5;
                    //using (new DriverCommon.SerializedAccess("get_Gain"))
                    {
                        // TODO: Can I set this any time?  Do we need more?
                        // TODO: Save time and what else to return later
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
                    DriverCommon.LogCameraMessage(0,"", "get_GainMax");
                    //return 5;
                    throw new ASCOM.PropertyNotImplementedException("GainMax", false);
				}
            }
        }

        public short GainMin
        {
            get
            {
                //using (new DriverCommon.SerializedAccess("get_GainMin"))
                {
                    DriverCommon.LogCameraMessage(0,"", "get_GainMin");
                    //return 0;
                    throw new ASCOM.PropertyNotImplementedException("GainMin", true);
				}
            }
        }

        public IStateValueCollection DeviceState
        {
            get
            {
                List<IStateValue>
                deviceState = new List<IStateValue>();

                try { deviceState.Add(new StateValue(nameof(ICameraV4.CameraState), CameraState)); } catch { }
                try { deviceState.Add(new StateValue(nameof(ICameraV4.ImageReady), ImageReady)); } catch { }
                try { deviceState.Add(new StateValue(nameof(ICameraV4.PercentCompleted), PercentCompleted)); } catch { }
                // try { deviceState.Add(new StateValue(nameof(IFocuserV4.Temperature), Temperature)); } catch { }
                // try { deviceState.Add(new StateValue(DateTime.Now)); } catch { }

                return new StateValueCollection(deviceState);
            }
        }
        public ArrayList Gains
        {
            get
            {
//               using (new DriverCommon.SerializedAccess("get_Gains"))
               {
                    DriverCommon.LogCameraMessage(0,"", "get_Gains");
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
                    DriverCommon.LogCameraMessage(0,"", "get_HasShutter");
                    // We can't do dark frames
                    return false;
				}
            }
        }

        public double HeatSinkTemperature
        {
            get
            {
                DriverCommon.LogCameraMessage(0,"", "get_HeatSinkTemperature");
                throw new ASCOM.PropertyNotImplementedException("HeatSinkTemperature", false);
            }
        }

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
            int MSensorWidthPx = DriverCommon.Settings.Info.ImageWidthPixels;
            int MSensorHeightPx = DriverCommon.Settings.Info.ImageHeightPixels;
            // TODO: Should be returned based on image size
            int[,,] rgbImage= new int[MSensorWidthPx, MSensorHeightPx, 3]; // Assuming this is declared and initialized elsewhere.


            // Wait for the file to be closed and available.
            while (!IsFileClosed(MNewFile)) { }
            rgbImage = _imageDataProcessor.ReadRawPentax(MNewFile);

            result = Resize(rgbImage, 3, StartX, StartY, NumX, NumY);
            return result;
        }

        private object ReadImageFileRGGB(string MNewFile)
        {
            object result = null;
            //Bitmap _bmp;
            int MSensorWidthPx = DriverCommon.Settings.Info.ImageWidthPixels;
            int MSensorHeightPx = DriverCommon.Settings.Info.ImageHeightPixels;
            // TODO: Should be returned based on image size
            int[,] rgbImage = new int[MSensorWidthPx, MSensorHeightPx]; // Assuming this is declared and initialized elsewhere.


            // Wait for the file to be closed and available.
            while (!IsFileClosed(MNewFile)) { }
            rgbImage = _imageDataProcessor.ReadRBBGPentax(MNewFile);

            // TODO: Sharpcap problem
            result = Resize(rgbImage, 2, StartX, StartY, NumX, NumY);
            return result;
        }

        private object ReadImageFileQuick(string MNewFile)
        {
            object result = null;
            Bitmap _bmp;
            int MSensorWidthPx = DriverCommon.Settings.Info.ImageWidthPixels;
            int MSensorHeightPx = DriverCommon.Settings.Info.ImageHeightPixels;
            // TODO: Should be returned based on image size
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

            int scale = 1;

            if (DriverCommon.Settings.DefaultReadoutMode==PentaxKPProfile.OUTPUTFORMAT_RAWBGR||
                DriverCommon.Settings.DefaultReadoutMode == PentaxKPProfile.OUTPUTFORMAT_RGGB)
                scale = 256;

                for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    _cameraImageArray[x, y, 0] = scale*Marshal.ReadByte(ptr, (stride * y) + (4 * x));
                    _cameraImageArray[x, y, 1] = scale * Marshal.ReadByte(ptr + 1, (stride * y) + (4 * x));
                    _cameraImageArray[x, y, 2] = scale * Marshal.ReadByte(ptr + 2, (stride * y) + (4 * x));
                }
            }

            if(DriverCommon.Settings.DefaultReadoutMode == PentaxKPProfile.OUTPUTFORMAT_RGGB)
            {
                int[,] _cameraImageArray2 = new int[_bmp.Width, _bmp.Height]; // Assuming this is declared and initialized elsewhere.

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (y % 2 == 0 && x % 2 == 0)
                            // Red
                            _cameraImageArray2[x, y] = _cameraImageArray[x, y, 2];
                        if (y % 2 == 0 && x % 2 == 1)
                            // Green
                            _cameraImageArray2[x, y] = _cameraImageArray[x, y, 1];
                        if (y % 2 == 1 && x % 2 == 0)
                            // Green
                            _cameraImageArray2[x, y] = _cameraImageArray[x, y, 1];
                        if (y % 2 == 1 && x % 2 == 1)
                            // Blue
                            _cameraImageArray2[x, y] = _cameraImageArray[x, y, 0];
                    }
                }

                // Unlock the bits.
                _bmp.UnlockBits(bmpData);
                DriverCommon.LogCameraMessage(0,"Image", "Resize2");
                result = Resize(_cameraImageArray2, 2, StartX, StartY, NumX, NumY);
                return result;
            }

            // Unlock the bits.
            _bmp.UnlockBits(bmpData);
            DriverCommon.LogCameraMessage(0,"Image", "Resize");
            result = Resize(_cameraImageArray, 3, StartX, StartY, NumX, NumY);
            return result;
        }

        public object ImageArray
        {
            get
            {
//                using (new SerializedAccess(this, "get_ImageArray",false))
                {
                    object result;

                    DriverCommon.LogCameraMessage(0,"", "get_ImageArray");
                    String imageName;
                    BitmapImage bitmap;
                    while(bitmapsToProcess.Count != 0)
                    {
                        bitmap = bitmapsToProcess.Dequeue();
                        DriverCommon.LogCameraMessage(0,"", "Calling ReadImageQuick");

                        result=ReadImageQuick(bitmap);
                        if (bitmapsToProcess.Count == 0)
                            return result;
                    }

                    while (imagesToProcess.Count != 0)
                    {
                        imageName = imagesToProcess.Dequeue();
                        if (imageName.Substring(imageName.Length - 3) == "JPG")
                        {
                            DriverCommon.LogCameraMessage(0,"", "Calling ReadImageFileQuick");
                            result = ReadImageFileQuick(imageName);
                            while (!IsFileClosed(imageName)) { }
                            File.Delete(imageName);
                            if (imagesToProcess.Count == 0)
                                return result;
                        }

                        if (imageName.Substring(imageName.Length - 3) == "DNG")
                        {
                            if (DriverCommon.Settings.DefaultReadoutMode == PentaxKPProfile.OUTPUTFORMAT_RAWBGR)
                            {
                                DriverCommon.LogCameraMessage(0,"", "Calling ReadImageFileRAW");
                                result = ReadImageFileRaw(imageName);
                                while (!IsFileClosed(imageName)) { }
                                File.Delete(imageName);
                                if (imagesToProcess.Count == 0)
                                    return result;
                            }
                            else
                            {
                                DriverCommon.LogCameraMessage(0,"", "Calling ReadImageFileRGGB");
                                result = ReadImageFileRGGB(imageName);
                                while (!IsFileClosed(imageName)) { }
                                File.Delete(imageName);
                                if (imagesToProcess.Count == 0)
                                    return result;
                            }

                        }

                        throw new ASCOM.PropertyNotImplementedException("ImageArray", false);
                    }

                    throw new ASCOM.PropertyNotImplementedException("ImageArray", false);

                }
            }
        }

        public object ImageArrayVariant
        {
            get
            {
                //using (new SerializedAccess(this, "get_ImageArrayVariant"))
                {
                    DriverCommon.LogCameraMessage(0,"", "get_ImageArrayVariant");
                    throw new ASCOM.PropertyNotImplementedException("ImageArrayVariant", false);

                }
            }
        }

        public bool ImageReady
        {
            get
            {
                //using (new DriverCommon.SerializedAccess("get_ImageReady", true))
                // TODO:  not thread safe
                {
                    DriverCommon.LogCameraMessage(0,"", 
                        "get_ImageReady Images "+imagesToProcess.Count.ToString()+" Bitmaps "+bitmapsToProcess.Count.ToString());
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
                    DriverCommon.LogCameraMessage(0,"", "get_IsPulseGuiding");
                    throw new ASCOM.PropertyNotImplementedException("IsPulseGuiding", false);
				}
            }
        }

        public double LastExposureDuration
        {
            get
            {
                DriverCommon.LogCameraMessage(0, "", "get_LastExposureDuration");
                return previousDuration;
            }
        }

        public string LastExposureStartTime
        {
            get
            {
                // TODO: Last exposure start time
                //using (new SerializedAccess(this, "get_LastExposureStartTime"))
                {
                    DriverCommon.LogCameraMessage(0,"", "get_LastExposureStartTime");
                    string exposureStartString = lastCaptureStartTime.ToString("yyyy-MM-ddTHH:mm:ss");
                    return exposureStartString;
                }
            }
        }

        public int MaxADU
        {
            get
            {
                //using (new SerializedAccess(this, "get_MaxADU"))
                {
                    DriverCommon.LogCameraMessage(0,"", "get_MaxADU");
                    int bpp = 8;
                    if (DriverCommon.Settings.DefaultReadoutMode==PentaxKPProfile.OUTPUTFORMAT_RGGB|| DriverCommon.Settings.DefaultReadoutMode == PentaxKPProfile.OUTPUTFORMAT_RAWBGR)
                        bpp = 16;
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
                    DriverCommon.LogCameraMessage(0,"", "get_MaxBinX");
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
                    DriverCommon.LogCameraMessage(0,"", "get_MaxBinY");
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
                    DriverCommon.LogCameraMessage(0,"", "get_NumX "+RequestedWidth.ToString());
                    return RequestedWidth;
				}
            }
            set
            {
                //using (new SerializedAccess(this, "set_NumX"))
                {
                    DriverCommon.LogCameraMessage(0,"", "set_NumX " + value.ToString() + " CameraXSize " + MaxImageWidthPixels);
                    RequestedWidth = value;
                    //RequestedWidth = MaxImageWidthPixels;
				}
            }
        }

        public int NumY
        {
            get
            {
                //using (new SerializedAccess(this, "get_NumY"))
                {
                    DriverCommon.LogCameraMessage(0,"", "get_NumY "+RequestedHeight.ToString());
                    return RequestedHeight;
                }
            }
            set
            {
                //using (new SerializedAccess(this, "set_NumY"))
                {
                    DriverCommon.LogCameraMessage(0,"", "set_NumY " + value.ToString() + " CameraYSize " + MaxImageHeightPixels);
                    RequestedHeight = value;
                    //RequestedHeight = MaxImageHeightPixels;
				}
            }
        }

        public short PercentCompleted
        {
            get
            {
               //using (new SerializedAccess(this, "get_PercentCompleted"))
                {
                    DriverCommon.LogCameraMessage(0,"", "get_PercentCompleted");
                    TimeSpan diff = DateTime.Now.Subtract(lastCaptureStartTime);
                    double percentage=((double)diff.Seconds+((double)diff.Milliseconds/1000.0))*100.0/previousDuration;
                    return (short)percentage;

                }
            }
        }

        public double PixelSizeX
        {
            get
            {
                 //using (new SerializedAccess(this, "get_PixelSizeX"))
                {
                    DriverCommon.LogCameraMessage(0,"", "get_PixelSizeX");
                    return DriverCommon.Settings.Info.PixelWidth;
				}
            }
        }

        public double PixelSizeY
        {
            get
            {
                //using (new SerializedAccess(this, "get_PixelSizeY"))
                {
                    DriverCommon.LogCameraMessage(0,"", "get_PixelSizeY");
                    return DriverCommon.Settings.Info.PixelHeight;
				}
            }
        }

        public void PulseGuide(GuideDirections Direction, int Duration)
        {
            //using (new SerializedAccess(this, "PulseGuide()"))
            {
                DriverCommon.LogCameraMessage(0,"", "PulseGuide()");
                throw new ASCOM.MethodNotImplementedException("PulseGuide");
            }
        }

        public short ReadoutMode
        {
            get
            {
                //using (new SerializedAccess(this, "get_ReadoutMode"))
                {
                    DriverCommon.LogCameraMessage(0,"", "get_ReadoutMode");
                    return (short)m_readoutmode;
				}
            }
            set
            {
                //using (new DriverCommon.SerializedAccess("set_ReadoutMode"))
                {
                    DriverCommon.LogCameraMessage(0,"", "ReadoutMode Set "+value.ToString());
                    if (m_captureState != CameraStates.cameraIdle)
                        throw new ASCOM.InvalidOperationException("Call to set_ReadoutMode when camera not Idle!");

                    if (ReadoutModes.Count > value)
                    {
                        switch (value)
                        {
                            case 0:
                                FastReadout = false;
                                m_readoutmode = 0;
                                if(DriverCommon.Settings.UseLiveview)
                                          DriverCommon.m_camera.StartLiveView();
                                MaxImageWidthPixels = DriverCommon.Settings.Info.ImageWidthPixels; // Constants to define the ccd pixel dimenstion
                                MaxImageHeightPixels = DriverCommon.Settings.Info.ImageHeightPixels;
                                //StartX = 0;
                                //StartY = 0;
                                //NumX = MaxImageWidthPixels;
                                //NumY = MaxImageHeightPixels;
                                break;

                            case 1:
                                m_readoutmode = 1;
                                FastReadout = true;
                                MaxImageWidthPixels = DriverCommon.Settings.Info.LiveViewWidthPixels; // Constants to define the ccd pixel dimenstion
                                MaxImageHeightPixels = DriverCommon.Settings.Info.LiveViewHeightPixels;
                                //StartX = 0;
                                //StartY = 0;
                                //NumX = MaxImageWidthPixels;
                                //NumY = MaxImageHeightPixels;
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
                    DriverCommon.LogCameraMessage(0,"","get_ReadoutModes");

                    ArrayList modes = new ArrayList();

                    modes.Add(String.Format("Full Resolution ({0} x {1})", DriverCommon.Settings.Info.ImageWidthPixels, DriverCommon.Settings.Info.ImageHeightPixels));
                    modes.Add(String.Format("LiveView ({0} x {1})", DriverCommon.Settings.Info.LiveViewWidthPixels, DriverCommon.Settings.Info.LiveViewHeightPixels));

                    return modes;
				}
            }
        }

        public string SensorName
        {
            get
            {
                //using (new DriverCommon.SerializedAccess("get_SensorName"))
                {
                    DriverCommon.LogCameraMessage(0,"", "get_SensorName");
                    // TODO: Add this info somewhere
                    return "";// "QHY247C";// "IMX271";
				}
            }
        }

        public SensorType SensorType
        {
            get
            {
                //using (new SerializedAccess(this, "get_SensorType"))
                {
                    DriverCommon.LogCameraMessage(0,"", "get_SensorType");
                    if (DriverCommon.Settings.DefaultReadoutMode == PentaxKPProfile.OUTPUTFORMAT_RGGB)
                        return SensorType.RGGB;
                    else
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
                    DriverCommon.LogCameraMessage(0,"", "get_SetCCDTemperature");
                    throw new ASCOM.PropertyNotImplementedException("SetCCDTemperature", false);
				}
            }
            set
            {
               //using (new SerializedAccess(this, "set_SetCCDTemperature"))
                {
                    DriverCommon.LogCameraMessage(0,"", "set_SetCCDTemperature");
                    throw new ASCOM.PropertyNotImplementedException("SetCCDTemperature", true);
				}
            }
        }

/*        private void StopThreadCapture()
        {
            // Can't stop capture
            DriverCommon.m_camera.StopCapture();

            m_captureState = CameraStates.cameraIdle;
        }*/

/*        private async void btnStopCaptureOnClick(object sender, RoutedEventArgs e)
        {
            if (camera == null) { return; }
            progress.Report("StopCapture.");
            await Task.Run(() =>
            {
                camera.StopCapture();
                progress.Report("Capture has been stopped.");
            });
        } */


        /*        private void StopThreadCapture()
                {
                    DriverCommon.LogCameraMessage(0,"", "StopThreadCapture");

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
                            //m_captureState = Ricoh.CameraController.CaptureState.Unknown;

                            while (DriverCommon.m_camera.Status.CurrentCapture == null)
                               sleepReturn = _requestTermination.WaitOne(20);

                            if (!sleepReturn)
                            {
                                m_captureState = Ricoh.CameraController.CaptureState.Executing;

                                while (DriverCommon.m_camera.Status.CurrentCapture.State != Ricoh.CameraController.CaptureState.Complete)
                                {
                                    DriverCommon.LogCameraMessage(1,"long running task", DriverCommon.m_camera.Status.CurrentCapture.State.ToString() + " " + Ricoh.CameraController.CaptureState.Complete.ToString());
                                    if (_requestTermination.WaitOne(20))
                                        break;
                                }
                                m_captureState = DriverCommon.m_camera.Status.CurrentCapture.State;
                            }
                            else
                                m_captureState = Ricoh.CameraController.CaptureState.Unknown;
                        }

                        DriverCommon.LogCameraMessage(0,"long running task", "exiting "+m_captureState.ToString());
                    });

                    cameraThread.SetApartmentState(ApartmentState.MTA);
                    cameraThread.Priority = ThreadPriority.Lowest;
                    cameraThread.Start();
                }
        */
        public async void StartExposure(double Duration, bool Light)
        {
            // Set it right away
            // TODO: Maybe use the starting state
            DriverCommon.LogCameraMessage(0, "", "StartExposure()");

            //Check duration range and save 
            if (Duration <= 0.0)
            {
                throw new InvalidValueException("StartExposure", "Duration", " > 0");
            }


            // Light or dark frame
            // TODO:  I think we need to update the state back and forth for LastSetFastReadout
            //          using (new DriverCommon.SerializedAccess("StartExposure()"))
            await Task.Run(() =>
            {
                while (m_captureState != CameraStates.cameraIdle)
                    Thread.Sleep(100);

                imagesToProcess.Clear();
                m_captureState = CameraStates.cameraWaiting;

                if (LastSetFastReadout)
                {
                    //No need to start exposure
                    DriverCommon.LogCameraMessage(0, "", "StartExposure() fast");
                    if (Duration <= 0.0)
                    {
                        throw new InvalidValueException("StartExposure", "Duration", " > 0");
                    }

                    m_captureState = CameraStates.cameraExposing;
                    previousDuration = Duration;
                    return;
                }

                ShutterSpeed shutterSpeed;
                shutterSpeed = ShutterSpeed.SS1_24000;
                if (Duration > 1.0 / 20000.0 - 0.000001)
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
                // TODO: add additional times 
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
                if (Duration > 9.99)
                    shutterSpeed = ShutterSpeed.SS10;
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

                FNumber fNumber = new FNumber();
                DriverCommon.m_camera.GetCaptureSettings(new List<CaptureSetting>() { fNumber });
                List<CaptureSetting> availableFNumberSettings = fNumber.AvailableSettings;

                //Number fNumber = FNumber.F5_6;
                //cameraDevice.SetCaptureSettings(new List<CaptureSetting>() { fNumber });

                // The list above might contain the following values.
                // F4.0 (F4_0), F4.5 (F4_5), F5.0 (F5_0)


                StartCaptureResponse response = DriverCommon.m_camera.StartCapture(false);
                lastCaptureResponse=response.Capture.ID;
                // Clear existing images
//                if (response.Equals(Response.OK))
                {
                    previousDuration = Duration;
                    lastCaptureStartTime = DateTime.Now;
                    // Make sure we don't change a reading to exposing
                    if (m_captureState == CameraStates.cameraWaiting)
                        m_captureState = CameraStates.cameraExposing;
                }
//                else
                {
 //                   m_captureState = CameraStates.cameraError;
 //                   throw new ASCOM.InvalidOperationException("Call to StartExposure SDK not successful");
                }
            });
    }

    public int StartX
    {
        get
        {
                //using (new SerializedAccess(this, "get_StartX"))
                {
//                    DriverCommon.LogCameraMessage(0,"StartX Get", RequestedStartX.ToString());
                    DriverCommon.LogCameraMessage(0,"", "get_StartX");
                    return RequestedStartX;
				}
        }
        set
        {
               //using (new SerializedAccess(this, "set_StartX"))
                {
                    DriverCommon.LogCameraMessage(0,"", "set_StartX "+value.ToString());
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
                    DriverCommon.LogCameraMessage(0,"", "get_StartY");
                    return RequestedStartY;
				}
        }
        set
        {
                //using (new SerializedAccess(this, "set_StartY"))
                {
                    DriverCommon.LogCameraMessage(0,"", "set_StartY "+value.ToString());
                    RequestedStartY = value;
				}
        }
    }

        public async void StopExposure()
        {
            // TODO: fix stop exposure
            // Note that StopExposure does not try to stop the current exposure
            DriverCommon.LogCameraMessage(0,"", "StopExposure");
            if (LastSetFastReadout)
            {
                m_captureState = CameraStates.cameraIdle;
                return;
            }

            // TODO: cameraWaiting is bad because it will get set to other
            if (m_captureState != CameraStates.cameraExposing&& m_captureState != CameraStates.cameraWaiting)
                return;

            await Task.Run(() =>
            {
                while(m_captureState!= CameraStates.cameraExposing)
                {
                    DriverCommon.LogCameraMessage(0, "StopExposure", "Waiting for camera to be in exposing.");
                    Thread.Sleep(100);
                }

                canceledCaptureResponse = lastCaptureResponse;
                if (previousDuration > 5)
                {
                    DriverCommon.m_camera.Disconnect(Ricoh.CameraController.DeviceInterface.USB);
                    m_captureState = CameraStates.cameraError;
                    return;
                }

                while (m_captureState == CameraStates.cameraExposing)
                {
                    Thread.Sleep(100);
                    DriverCommon.LogCameraMessage(0, "StopExposure", "Waiting for capture to finish.");
                }

                return;

                /*                DriverCommon.LogCameraMessage(0, "StopExposure", "Stopping Capture.");
                                Response response = DriverCommon.m_camera.StopCapture();
                                if (response.Result==Result.OK)
                                {
                                    DriverCommon.LogCameraMessage(0, "StopExposure", "Capture has been stopped.");
                                    m_captureState = CameraStates.cameraReading;
                                    return;
                                }
                                DriverCommon.LogCameraMessage(0, "StopExposure", "Failed. " + response.Errors.First().Message);
                                return;*/
            });
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
                // TODO: register in installer 
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
                //using (new DriverCommon.SerializedAccess("IsConnected"))
                {
                    if (DriverCommon.m_camera == null)
                        return false;

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
                DriverCommon.LogCameraMessage(0,"CheckConnected", message);
                throw new ASCOM.NotConnectedException(message);
            }
        }

        #endregion

        internal static object Resize(object array, int rank, int startX, int startY, int width, int height)
        {

            if (rank == 2)
            {
                int[,] input = (int[,])array;
                DriverCommon.LogCameraMessage(0, "Resize", string.Format("rank={0}, startX={1}, startY={2}, width={3}, height={4} owidth={5} oheight={6}", rank, startX, startY, width, height, input.GetLength(0).ToString(), input.GetLength(1).ToString()));

                if (startX == 0 && startY == 0 && width >= input.GetLength(0) && height >= input.GetLength(1))
                {
                    DriverCommon.LogCameraMessage(0, "Resize", "returning original values");
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

                DriverCommon.LogCameraMessage(0, "Resize", string.Format("rank={0}, startX={1}, startY={2}, width={3}, height={4} owidth={5} oheight={6}", rank, startX, startY, width, height, input.GetLength(0).ToString(), input.GetLength(1).ToString()));

                if (startX == 0 && startY == 0 && width >= input.GetLength(0) && height >= input.GetLength(1))
                {
                    DriverCommon.LogCameraMessage(0,"Resize","returning original values");
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
