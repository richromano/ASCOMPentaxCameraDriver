using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;

using ASCOM.Utilities;
using System.Threading;
using System.Collections.ObjectModel;

namespace ASCOM.PentaxKP
{
    public class PentaxKPProfile
    {
        //        private static int DefaultImageWidth = 6016; // Constants to define the ccd pixel dimenstions
        //        private static int DefaultImageHeight = 4000;
//        protected const UInt32 IMAGEMODE_RAW = 1;
//        protected const UInt32 IMAGEMODE_RGB = 2;

        public const int PERSONALITY_SHARPCAP = 0;
        public const int PERSONALITY_NINA = 1;
        public const short OUTPUTFORMAT_RAWBGR = 0;
        public const short OUTPUTFORMAT_BGR = 1;
        public const short OUTPUTFORMAT_RGGB = 2;

        private DeviceInfo m_info;

        public bool EnableLogging = false;
        public int DebugLevel = 0;
        public string DeviceId = "";
        public int DeviceIndex = 0;
        public short DefaultReadoutMode = PentaxKPProfile.OUTPUTFORMAT_RAWBGR;
        public bool UseLiveview = true;
        public int Personality = PERSONALITY_SHARPCAP;
        public bool AutoLiveview = false;
        public bool BulbModeEnable = false;
        public short BulbModeTime = 1;
        public bool AllowISOAdjust = false;
        public bool UsingCameraLens = false;
        public string LensId = "";
        public bool HandsOffFocus = false;

        // Dynamic values
        //        public int ImageWidth = DefaultImageWidth; // Initialise variables to hold values required for functionality tested by Conform
        //        public int ImageHeight = DefaultImageHeight;
        //        public int ImageXOffset = 0;
        //        public int ImageYOffset = 0;

        public void assignCamera(int index)
        {
            m_info.ImageWidthPixels = PentaxCameraInfo.ElementAt(index).ImageWidthPixels;
            m_info.ImageHeightPixels = PentaxCameraInfo.ElementAt(index).ImageHeightPixels;
            m_info.LiveViewWidthPixels = PentaxCameraInfo.ElementAt(index).LiveViewWidthPixels;
            m_info.LiveViewHeightPixels = PentaxCameraInfo.ElementAt(index).LiveViewHeightPixels;
            m_info.PixelWidth = PentaxCameraInfo.ElementAt(index).PixelWidth;
            m_info.PixelHeight = PentaxCameraInfo.ElementAt(index).PixelHeight;
        }

        public void assignCamera(string name)
        {
            for (int i = 0; i < PentaxCameraInfo.Count; i++)
            {
                DriverCommon.LogCameraMessage(0,"assignCamera", PentaxCameraInfo.ElementAt(i).label+" "+name);
                if (PentaxCameraInfo.ElementAt(i).label == name)
                {
                    assignCamera(i);
                    return;
                }
            }

            assignCamera(0);
            return;
        }

        public struct DeviceInfo
        {
            public int Version;
            public int ImageWidthPixels;
            public int ImageHeightPixels;
            public int LiveViewHeightPixels;
            public int LiveViewWidthPixels;
            //            public int BayerXOffset;
            //            public int BayerYOffset;
            //            public int ExposureTimeMin;
            //            public int ExposureTimeMax;
            //            public int ExposureTimeStep;
            public double PixelWidth;
            public double PixelHeight;
            //            public int BitsPerPixel;

            public string Manufacturer;
            public string Model;
            public string SerialNumber;
            public string DeviceName;
            public string SensorName;
            public string DeviceVersion;
        }

        public struct CameraInfo
        {
            internal readonly string label;
            internal readonly int id;
            internal readonly int ImageWidthPixels;
            internal readonly int ImageHeightPixels;
            internal readonly int LiveViewWidthPixels;
            internal readonly int LiveViewHeightPixels;
            internal readonly double PixelWidth;
            internal readonly double PixelHeight;

            public CameraInfo(string label, int id, int ImageWidthPixels, int ImageHeightPixels, int LiveViewWidthPixels, int LiveViewHeightPixels, double PixelWidth, double PixelHeight)
            {
                this.label = label;
                this.id = id;
                this.ImageWidthPixels = ImageWidthPixels;
                this.ImageHeightPixels = ImageHeightPixels;
                this.LiveViewWidthPixels = LiveViewWidthPixels;
                this.LiveViewHeightPixels = LiveViewHeightPixels;
                this.PixelWidth = PixelWidth;
                this.PixelHeight = PixelHeight;
            }

            public string Label { get { return label; } }
            public int Id { get { return id; } }

        }

        // KP 6016x4000 14bit
        // K70 6000x4000 14bit
        // KF 6000x4000 14bit
        // K1ii 7360x4912 14bit
        // K1  7360x4912 14bit
        // K3iii 6192x4128 14bit 
        // 645Z 8256x6192 14bit

        static readonly IList<CameraInfo> PentaxCameraInfo = new ReadOnlyCollection<CameraInfo>
            (new[] {
                // TODO: fix preview size
             new CameraInfo ("PENTAX KP", 0, 6016, 4000, 720, 480, 3.88, 3.88),
             new CameraInfo ("PENTAX K-70", 1, 6000, 4000, 720, 480, 3.88, 3.88),
             new CameraInfo ("PENTAX KF", 2, 6000, 4000, 720, 480, 3.88, 3.88),
             new CameraInfo ("PENTAX K-1II", 3, 7360, 4912, 720, 480, 4.86, 4.86),
             new CameraInfo ("PENTAX K-1", 4, 7360, 4912, 720, 480, 4.86, 4.86),
             new CameraInfo ("PENTAX K-3III", 5, 6192, 4128, 720, 480, 3.75, 3.75),
             new CameraInfo ("PENTAX 645Z", 6, 8256, 6192, 720, 480, 5.32, 5.32)
            });

        public DeviceInfo Info
        {
            get
            {
                return m_info;
            }
        }

        public String SerialNumber
        {
            get
            {
                return m_info.SerialNumber.TrimStart(new char[] { '0' });
            }
        }

        public String DisplayName
        {
            get
            {
                return String.Format("{0} (s/n: {1})", "Pentax KP", SerialNumber);
            }
        }

        public String Model
        {
            get
            {
                return m_info.Model;
            }
        }




    }

    class DriverCommon
    {
        private static Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        public static string DriverVersion = string.Format(CultureInfo.InvariantCulture, "{0}.{1}.{2}.{3}", version.Major, version.Minor, version.Build, version.Revision);

        // IMPORTANT
        // CameraDriverName **cannot** change, the APT software recognizes this name specifically and enables fast-readout
        // for preview mode.
        // "Pentax KP Camera"
        public static string CameraDriverName = "Pentax KP/K1 Camera";
        public static string CameraDriverId = "ASCOM.PentaxKP.Camera";
        public static string CameraDriverDescription = "Pentax KP/K1 Camera";
        public static string CameraDriverInfo = $"Camera control for Pentax KP/K1 cameras. Version: {DriverVersion}";

        public static string FocuserDriverName = "Pentax KP/K1 Lens Focuser";
        public static string FocuserDriverId = "ASCOM.PentaxKP.Focuser";
        public static string FocuserDriverDescription = "Pentax KP/K1 Camera Focuser";
        public static string FocuserDriverInfo = $"Focuser  that allows connection to a camera-controlled autofocus lens. Version: {DriverVersion}";

        public static PentaxKPProfile Settings = new PentaxKPProfile();
        private static TraceLogger Logger = new TraceLogger("", "PentaxKP");

        internal static Ricoh.CameraController.CameraDevice m_camera = null;

        // Common to both
        internal static string traceStateProfileName = "Trace Level";
        internal static string traceStateDefault = "false";
        internal static string cameraProfileName = "Camera ID";
        internal static string cameraDefault = "";

        // Specific to Camera
        internal static string readoutModeDefaultProfileName = "Readout Mode";
        internal static string readoutModeDefault = "2";
        internal static string useLiveviewProfileName = "Use Camera Liveview";
        internal static string useLiveviewDefault = "true";
        internal static string autoLiveviewProfileName = "Auto Liveview";
        internal static string autoLiveviewDefault = "false";
        internal static string personalityProfileName = "Personality";
        internal static string personalityDefault = "0"; // TODO:  why is this one?
        internal static string bulbModeEnableProfileName = "Bulb Mode Enable";
        internal static string bulbModeEnableDefault = "true";
        internal static string bulbModeTimeProfileName = "Bulb Mode Time";
        internal static string bulbModeTimeDefault = "1";
        internal static string allowISOAdjustProfileName = "Allow ISO Adjust";
        internal static string allowISOAdjustDefault = "false";

        // Specific to Focuser
        internal static string lensIdProfileName = "Lens ID";
        internal static string lensIdProfileDefault = "";
        internal static string usingCameraLensProfileName = "Using Camera Lens";
        internal static string usingCameraLensProfileDefault = "false";
        internal static string handsOffProfileName = "Hands Off";
        internal static string handsOffProfileDefault = "false";

        static public bool CameraConnected
        {
            get
            {
                // TODO:  this is not Mutex safe
                //using (new DriverCommon.SerializedAccess("get_Connected"))
                {
                    DriverCommon.LogCameraMessage(0,"Connected", "get");
                    if (DriverCommon.m_camera == null)
                        return false;

                    return DriverCommon.m_camera.IsConnected(Ricoh.CameraController.DeviceInterface.USB);
                }
            }

/*            set
            {
                bool oldValue = cameraConnected;

                cameraConnected = value;

                try
                {
                    EnsureCameraConnection();
                }
                catch
                {
                    cameraConnected = oldValue;
                }
            }*/
        }

        static public bool FocuserConnected
        {
            get
            {
                // TODO:  this is not Mutex safe
                //using (new DriverCommon.SerializedAccess("get_Connected"))
                {
                    DriverCommon.LogCameraMessage(0,"Connected", "get");
                    if (DriverCommon.m_camera == null)
                        return false;

                    return DriverCommon.m_camera.IsConnected(Ricoh.CameraController.DeviceInterface.USB);
                }
            }

/*            set
            {
                bool oldValue = focuserConnected;

                focuserConnected = value;

                try
                {
                    EnsureCameraConnection();
                }
                catch
                {
                    focuserConnected = oldValue;
                }
            }*/
        }

        public static void LogCameraMessage(int level, string identifier, string message, params object[] args)
        {
            if (level <= Settings.DebugLevel)
            {
                var msg = string.Format(message, args);
                Logger.LogMessage($"[camera] {identifier}", msg);
            }
        }

        public static void LogFocuserMessage(int level, string identifier, string message, params object[] args)
        {
            if (level <= Settings.DebugLevel)
            {
                var msg = string.Format(message, args);
                Logger.LogMessage($"[focuser] {identifier}", msg);
            }
        }

        private static void Log(String message, String source = "DriverCommon")
        {
            Logger.LogMessage(source, message);
        }

        public static bool ReadProfile()
        {
            // First read for camera, then read for focuser
            using (Profile driverProfile = new Profile())
            {
                driverProfile.DeviceType = "Camera";

                Settings.DeviceIndex = 0;
//                Settings.DebugLevel = Convert.ToBoolean(driverProfile.GetValue(CameraDriverId, traceStateProfileName, string.Empty, traceStateDefault));
                Settings.EnableLogging = Convert.ToBoolean(driverProfile.GetValue(CameraDriverId, traceStateProfileName, string.Empty, traceStateDefault));
                Settings.DeviceId = driverProfile.GetValue(CameraDriverId, cameraProfileName, string.Empty, cameraDefault);
                Settings.DefaultReadoutMode = Convert.ToInt16(driverProfile.GetValue(CameraDriverId, readoutModeDefaultProfileName, string.Empty, readoutModeDefault));
                Settings.UseLiveview = Convert.ToBoolean(driverProfile.GetValue(CameraDriverId, useLiveviewProfileName, string.Empty, useLiveviewDefault));
                Settings.Personality = Convert.ToInt16(driverProfile.GetValue(CameraDriverId, personalityProfileName, string.Empty, personalityDefault));
                Settings.AutoLiveview = Convert.ToBoolean(driverProfile.GetValue(CameraDriverId, autoLiveviewProfileName, string.Empty, autoLiveviewDefault));
                Settings.BulbModeEnable = Convert.ToBoolean(driverProfile.GetValue(CameraDriverId, bulbModeEnableProfileName, string.Empty, bulbModeEnableDefault));
                Settings.BulbModeTime = Convert.ToInt16(driverProfile.GetValue(CameraDriverId, bulbModeTimeProfileName, string.Empty, bulbModeTimeDefault));
                Settings.AllowISOAdjust = Convert.ToBoolean(driverProfile.GetValue(CameraDriverId, allowISOAdjustProfileName, string.Empty, allowISOAdjustDefault));
            }

            using (Profile driverProfile = new Profile())
            {
                driverProfile.DeviceType = "Focuser";

                Settings.UsingCameraLens = Convert.ToBoolean(driverProfile.GetValue(FocuserDriverId, usingCameraLensProfileName, string.Empty, usingCameraLensProfileDefault));
                Settings.LensId = driverProfile.GetValue(FocuserDriverId, lensIdProfileName, string.Empty, lensIdProfileDefault);
            }

            Logger.Enabled = Settings.EnableLogging;

            // TODO: : Add personality here

            Log($"DeviceID:                            {Settings.DeviceId}", "ReadProfile");
            Log($"Default Readout Mode:                {Settings.DefaultReadoutMode}", "ReadProfile");
            Log($"Use Liveview:                        {Settings.UseLiveview}", "ReadProfile");
            Log($"AutoLiveview @ 0.0s:                 {Settings.AutoLiveview}", "ReadProfile");
            Log($"Bulb Mode Enable:                    {Settings.BulbModeEnable}", "ReadProfile");
            Log($"Bulb Mode Time:                      {Settings.BulbModeTime}", "ReadProfile");
            Log($"Using Camera Lens:                   {Settings.UsingCameraLens}", "ReadProfile");
            Log($"FocuserDeviceID:                     {Settings.LensId}", "ReadProfile");
            Log($"User promises to not touch lens:     {Settings.HandsOffFocus}", "ReadProfile");

            return true;
        }

        public static bool WriteProfile()
        {
            using (Profile driverProfile = new Profile())
            {
                driverProfile.DeviceType = "Camera";
//                driverProfile.WriteValue(CameraDriverId, traceStateProfileName, Settings.DebugLevel.ToString());
                driverProfile.WriteValue(CameraDriverId, traceStateProfileName, Settings.EnableLogging.ToString());
                driverProfile.WriteValue(CameraDriverId, readoutModeDefaultProfileName, Settings.DefaultReadoutMode.ToString());
                driverProfile.WriteValue(CameraDriverId, useLiveviewProfileName, Settings.UseLiveview.ToString());
                driverProfile.WriteValue(CameraDriverId, autoLiveviewProfileName, Settings.AutoLiveview.ToString());
                driverProfile.WriteValue(CameraDriverId, personalityProfileName, Settings.Personality.ToString());
                driverProfile.WriteValue(CameraDriverId, bulbModeEnableProfileName, Settings.BulbModeEnable.ToString());
                driverProfile.WriteValue(CameraDriverId, bulbModeTimeProfileName, Settings.BulbModeTime.ToString());
                driverProfile.WriteValue(CameraDriverId, allowISOAdjustProfileName, Settings.AllowISOAdjust.ToString());

                if (Settings.DeviceId != null && Settings.DeviceId != "")
                {
                    driverProfile.WriteValue(CameraDriverId, cameraProfileName, Settings.DeviceId.ToString());
                }

                Logger.Enabled = Settings.EnableLogging;
            }

            using (Profile driverProfile = new Profile())
            {
                driverProfile.DeviceType = "Focuser";
                driverProfile.WriteValue(FocuserDriverId, lensIdProfileName, Settings.LensId);
                driverProfile.WriteValue(FocuserDriverId, usingCameraLensProfileName, Settings.UsingCameraLens.ToString());

                if (Settings.LensId != String.Empty)
                {
                    // We need to also set the value into the registry
     //               String key = $"HKEY_CURRENT_USER\\Software\\retro.kiwi\\SonyMTPCamera.dll\\Lenses\\{Settings.LensId}";

     //               Registry.SetValue(key, "Hands Off", Settings.HandsOffFocus ? 1 : 0);
                }
            }


            return true;
        }

/*        public class SerializedAccess : IDisposable
        {
            internal static Mutex m_serialAccess = new Mutex();

            internal String m_method;
            internal bool m_mustReleaseMutex;


            public SerializedAccess(String method, bool shortWait = true)
            {
                // Dont need camera
                // Need to know what kind of message
                m_method = method;
                m_mustReleaseMutex = true;
//                DriverCommon.LogCameraMessage(0,m_method, "[enter] " + m_serialAccess.ToString() + " " + shortWait.ToString());

                if (!m_serialAccess.WaitOne(10))
                {
//                    DriverCommon.LogCameraMessage(0,m_method, "Waiting to enter " + m_serialAccess.ToString());

                    if (shortWait)
                    {
                        m_mustReleaseMutex = false;
//                        DriverCommon.LogCameraMessage(0,m_method, "[in] short " + m_serialAccess.ToString());
                        return;
                    }
                    m_serialAccess.WaitOne(20000);
                }

//                DriverCommon.LogCameraMessage(0,m_method, "[in] " + m_serialAccess.ToString());
            }

            public void Dispose()
            {
                //DriverCommon.LogCameraMessage(0,m_method, "[out] " + m_serialAccess.ToString());
                if (m_mustReleaseMutex)
                    m_serialAccess.ReleaseMutex();
            }
        }*/


    }
}
