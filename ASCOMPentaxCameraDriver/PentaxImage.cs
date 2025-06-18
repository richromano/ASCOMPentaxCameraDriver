using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;
using ASCOM.Utilities;
using Ricoh.CameraController;

namespace ASCOM.PentaxKP
{
    public class PentaxKPImage : PentaxKPCommon
    {
        public enum ImageStatus
        {
            Created, Capturing, Reading, Ready, Failed, Cancelled
        }

        internal ImageInfo m_info;
        internal Ricoh.CameraController.CameraDevice m_camera;
        internal Ricoh.CameraController.StartCaptureResponse m_startCaptureResponse;
        public DateTime StartTime = DateTime.Now;
        private int m_personality;
        private short m_readoutMode;

        internal ImageStatus m_status = ImageStatus.Created;

        public int Width = 0;
        public int Height = 0;
        public static int[,,] RGB;
        public static int[,] BAYER;

        public PentaxKPImage(Ricoh.CameraController.CameraDevice camera, ImageInfo info, int personality, short readoutMode)
        {
            m_camera = camera;
            m_info = info;
            m_personality = personality;
            m_readoutMode = readoutMode;
            Status = PentaxKPImage.ImageStatus.Capturing;

            if (m_info.Status == STATUS_COMPLETE)
            {
                ProcessImageData();
                Status = PentaxKPImage.ImageStatus.Ready;
            }
        }

        public ImageStatus Status
        {
            get
            {
                //                uint battery=m_camera.Status.BatteryLevel;
                if (m_camera.IsConnected(Ricoh.CameraController.DeviceInterface.USB))
                    Log("Still connected");
                else
                    Log("Disconnected");

                switch (m_status)
                {
                    case ImageStatus.Created:
                    case ImageStatus.Ready:
                    case ImageStatus.Failed:
                    case ImageStatus.Cancelled:
                        Log(String.Format("get_Status - returning {0}", m_status.ToString()));
                        break;

                    case ImageStatus.Capturing:
                        // Figure out if we've finished
                        Log("get_Status - capturing, refreshing");
                        switch (m_camera.Status.CurrentCapture.State/*m_startCaptureResponse.Capture.State*/)
                        {
                            case Ricoh.CameraController.CaptureState.Executing:
                                m_status = ImageStatus.Capturing;
                                Log("get_Status - latest = Capturing");
                                break;

                            case Ricoh.CameraController.CaptureState.Complete:
                                m_status = ImageStatus.Reading;
                                Log("get_Status - latest = Reading");
                                ProcessImageData();
                                m_status = ImageStatus.Ready;
                                Log("get_Status - latest = Reading");
                                break;

                            default:
                                //Log(String.Format("get_Status - latest = unknown {0}", m_info.Status.ToString()));
                                break;
                        }

                                /*
                                                        switch (m_info.Status)
                                                        {
                                                            case STATUS_CANCELLED:
                                                                m_status = ImageStatus.Cancelled;
                                                                Log("get_Status - latest = Cancelled");
                                                                break;

                                                            case STATUS_FAILED:
                                                                m_status = ImageStatus.Failed;
                                                                Log("get_Status - latest = Failed");
                                                                break;

                                                            case STATUS_STARTING: // Getting ready to press button
                                                            case STATUS_EXPOSING: // Button is down
                                                            case STATUS_READING:  // Reading from camera, not driver
                                                                m_status = ImageStatus.Capturing;
                                                                Log("get_Status - latest = Capturing");
                                                                break;

                                                            case STATUS_COMPLETE:
                                                                m_status = ImageStatus.Reading;
                                                                Log("get_Status - latest = Reading");
                                                                ProcessImageData();
                                                                m_status = ImageStatus.Ready;
                                                                Log("get_Status - latest = Reading");
                                                                break;

                                                            default:
                                                                Log(String.Format("get_Status - latest = unknown {0}", m_info.Status.ToString()));
                                                                break;
                                                        }*/
                                     break;
                                }
                                return m_status;
            }

            set
            {
                m_status = value;
            }
        }

        public double Duration
        {
            get
            {
                return m_info.ExposureTime;
            }
        }

        public void ProcessImageData()
        {
            return;
        }

        public void Cleanup()
        {
            if (m_info.ImageData != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(m_info.ImageData);
                m_info.ImageData = IntPtr.Zero;
            }

            if (m_info.MetaData != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(m_info.MetaData);
                m_info.MetaData = IntPtr.Zero;
            }
        }

        private void Log(String message)
        {
            DriverCommon.LogCameraMessage("PentaxKPImage", message);
        }
    }
}
