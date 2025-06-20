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

        public enum ImageMode
        {
            RGB = (int)IMAGEMODE_RGB,
            RGGB = (int)IMAGEMODE_RAW,
        }

        public PentaxKPCamera(DeviceInfo info)
        {
            m_info = info;
        }

        public DeviceInfo Info
        {
            get
            {
                return m_info;
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
                return String.Format("{0} (s/n: {1})", "Pentax KP", SerialNumber);
            }
        }
    }
}
