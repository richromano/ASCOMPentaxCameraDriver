using System;
using System.Collections;
using System.Linq;
using System.IO;
using System.Windows.Media.Imaging;

namespace ASCOM.PentaxKP
{
    public class PentaxKPCamera : PentaxKPCommon
    {
        private DeviceInfo m_info;
        //internal PentaxKPImage m_lastImage = null;
        internal CaptureMode m_mode;
        internal ImageMode m_outputMode = ImageMode.RGB;
        internal CameraInfo m_resolutions;
        internal Boolean m_bulbMode = false;
        internal short m_bulbModeTime = 1;
        internal short m_desiredGain = -1;
        private ArrayList m_gains = new ArrayList();

        public enum ImageMode
        {
            RGB = (int)IMAGEMODE_RGB,
            RGGB = (int)IMAGEMODE_RAW,
        }

        public class CaptureMode
        {
            public Boolean Preview = false;
            public UInt32 ImageWidthPixels = 1;
            public UInt32 ImageHeightPixels = 1;
        }

        public PentaxKPCamera(DeviceInfo info)
        {
            m_info = info;
            m_mode = new CaptureMode();
            m_resolutions = new CameraInfo();

            m_mode.ImageWidthPixels = m_info.CropMode == 0 ? m_info.ImageWidthPixels : m_info.ImageWidthCroppedPixels;
            m_mode.ImageHeightPixels = m_info.CropMode == 0 ? m_info.ImageHeightPixels : m_info.ImageHeightCroppedPixels;
        }

        public Boolean BulbMode
        {
            get
            {
                return m_bulbMode;
            }

            set
            {
                m_bulbMode = value;
            }
        }

        public short BulbModeTime
        {
            get
            {
                return m_bulbModeTime;
            }

            set
            {
                m_bulbModeTime = value;
            }
        }

        public DeviceInfo Info
        {
            get
            {
                return m_info;
            }
        }

        public CaptureMode Mode
        {
            get
            {
                return m_mode;
            }
        }

        public void SetLens(string lensId)
        {
            //SetAttachedLens(m_handle, lensId);
        }

        public int GetFocusLimit()
        {
            return 0;//(int)GetFocusLimit(m_handle);
        }

        public int GetFocus()
        {
            return 0;//(int)GetFocusPosition(m_handle);
        }

        public void SetFocus(int position)
        {
            UInt32 focusPos = (UInt32)position;

            //SetFocusPosition(m_handle, ref focusPos);
        }

        public CameraInfo Resolutions
        {
            get
            {
                //GetCameraInfo(m_handle, ref m_resolutions, INFOFLAG_ACTIVE);

                return m_resolutions;
            }
        }

        public String Model
        {
            get
            {
                return m_info.Model;
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
                return String.Format("{0} (s/n: {1})", Model, SerialNumber);
            }
        }

        public Boolean HasLiveView
        {
            get
            {
                return (m_resolutions.CameraFlags & CAMERA_SUPPORTS_LIVEVIEW) != 0;
            }
        }

        public ImageMode OutputMode
        {
            get
            {
                return m_outputMode;
            }

            set
            {
                m_outputMode = value;
            }
        }

        private void Log(String message)
        {
            DriverCommon.LogCameraMessage("PentaxKPCamera", message);
        }
    }
}
