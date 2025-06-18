using Ricoh.CameraController;

using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using ASCOM.DeviceInterface;
using ASCOM.Utilities;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ASCOM.PentaxKP
{
    public class PentaxKPCamera : PentaxKPCommon
    {
        private DeviceInfo m_info;
        private CameraDevice camera = null;
        //internal PentaxKPImage m_lastImage = null;
        internal CaptureMode m_mode;
        internal ImageMode m_outputMode = ImageMode.RGB;
        internal CameraInfo m_resolutions;
        internal Dictionary<UInt32, CameraProperty> m_properties = new Dictionary<UInt32, CameraProperty>();
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

        public bool Connected
        {
            get
            {
                return camera != null;
            }

            set
            {
                if (value)
                {
                    if (camera == null)
                    {
//                                                m_handle = OpenDevice(m_info.DeviceName);
//                                                GetCameraInfo(m_handle, ref m_resolutions, 0);
                    	Log("Connecting...");
                        
                        camera = CameraDeviceDetector.Detect(Ricoh.CameraController.DeviceInterface.USB).FirstOrDefault();
                        if (camera != null)
                        {
                            //CameraEventListener cameraEventListener = new EventListener();
                            if (camera.EventListeners.Count == 0)
                                camera.EventListeners.Add(new EventListener());

                            var response = camera.Connect(Ricoh.CameraController.DeviceInterface.USB);
                            if (response.Equals(Response.OK))
                            {
                                Log("Connected. Model: " + camera.Model + ", SerialNumber:" + camera.SerialNumber);
                                m_resolutions.CameraFlags |= CAMERA_SUPPORTS_LIVEVIEW;
								//m_info doesn't have anything
                                m_resolutions.ImageHeightPixels = m_info.ImageHeightPixels;
                                m_resolutions.ImageWidthPixels = m_info.ImageWidthPixels;
                            }
                            else
                            {
                                Log("Connection is failed.");
                            }

                            response = camera.StartLiveView();
                        }
                        else
                        {
                           Log("Device has not found.");
                        }
                    }
                }
                else
                {
                    if (camera != null)
                    {
                        camera.Disconnect(Ricoh.CameraController.DeviceInterface.USB);
                    }

                    camera = null;
                    Log("Unable to connect to camera");
                }
            }
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
				//Need to fix this
                //image.Source = bitmapImage;
            }

		    // Image Added
		    public override void ImageAdded(CameraDevice sender, CameraImage image)
		    {
		        // Get the image and save it in the current directory
		        using (FileStream fs = new FileStream(
		            Environment.CurrentDirectory + Path.DirectorySeparatorChar +
		            image.Name, FileMode.Create, FileAccess.Write))
		        {
		            Response imageGetResponse = image.GetData(fs);
		            Console.WriteLine("Get Image is " +
		                (imageGetResponse.Result == Result.OK ?
		                    "SUCCEED." : "FAILED."));
		        }
		    }

            // Capture Complete
			 public override void CaptureComplete(CameraDevice sender, Capture capture)
			 {
                int y = 0;
                int x = 1 / y;
			     Console.WriteLine("Capture Complete. Capture ID: {0}", capture.ID);
			 }
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

        public ArrayList Gains
        {
            get
            {
                if (m_gains.Count == 0)
                {
                    CameraProperty iso = GetProperty(PentaxKPCommon.PROPERTY_ISO_OPTIONS);

                    PentaxKPCommon.PropertyValueOption[] options = iso.Options;

                    foreach (PentaxKPCommon.PropertyValueOption option in options)
                    {
                        // Unsure what the top-byte-set values are for, so filter them out
                        // 0x00ffffff = auto
                        if ((option.Value & 0xff000000) == 0)
                        {
                            m_gains.Add(option.Value.ToString());
                        }
                    }

                }

                return m_gains;
            }
        }

        public short GainIndex
        {
            get
            {
                CameraProperty iso = GetProperty(PentaxKPCommon.PROPERTY_ISO);

                UInt32 value = iso.CurrentValue(camera).Value;
                short index = (short)Gains.IndexOf(value.ToString());

                Log(String.Format("get GainIndex: camera reports '{0}', which maps to index {1}", value, index));

                return index;
            }

            set
            {
                if (value >= 0 && value < Gains.Count)
                {
                    m_desiredGain = value;
                }
                else if (value >= Gains.Count)
                {
                    Log(String.Format("Attempting to find gain {0} match", value));
                    int desiredIndex = Gains.IndexOf(value.ToString());

                    if (desiredIndex >= 0)
                    {
                        Log(String.Format("Setting gain to index {0} which matches {1}", desiredIndex, value));
                        m_desiredGain = (short)desiredIndex;
                    }
                }
            }
        }

        private void PopulatePropertyInfo()
        {
            if (m_properties.Count == 0)
            {
                // Need to actually fetch properties
                // Start with getting a list of all the properties
                UInt32 count = 0;

                // Get # of properties
                /*UInt32 hr = GetPropertyList(m_handle, IntPtr.Zero, ref count);
                int[] ids = new int[count];
                IntPtr pIds = Marshal.AllocCoTaskMem((int)count * sizeof(UInt32));

                hr = GetPropertyList(m_handle, pIds, ref count);

                Marshal.Copy(pIds, ids, 0, (int)count);
                Marshal.FreeCoTaskMem(pIds);

                // Now I want the property descriptors
                for (int i = 0; i < count; i++)
                {
                    PropertyDescriptor descriptor = new PropertyDescriptor();

                    hr = GetPropertyDescriptor(m_handle, (uint)ids[i], ref descriptor);

                    PropertyValueOption[] options = new PropertyValueOption[descriptor.ValueCount];

                    for (uint index = 0; index < descriptor.ValueCount; index++)
                    {
                        GetPropertyValueOption(m_handle, descriptor.Id, ref options[index], index);
//                        UInt32 countReturned = descriptor.ValueCount;
//                        GetPropertyValueOptions(m_handle, descriptor.Id, ref options, ref countReturned);
                    }

                    m_properties[descriptor.Id] = new CameraProperty(descriptor, options);
                }*/
            }
        }

        public Dictionary<UInt32, CameraProperty> Properties
        {
            get
            {
                PopulatePropertyInfo();

                return m_properties;
            }
        }

        public CameraProperty GetProperty(UInt32 id)
        {
            PopulatePropertyInfo();
            return m_properties.ContainsKey(id) ? m_properties[id] : null;
        }

        public PropertyValue GetPropertyValue(UInt32 id)
        {
            PopulatePropertyInfo();
            return m_properties.ContainsKey(id) ? m_properties[id].CurrentValue(camera) : new PropertyValue();
        }

        private void Log(String message)
        {
            DriverCommon.LogCameraMessage("PentaxKPCamera", message);
        }
    }
}
