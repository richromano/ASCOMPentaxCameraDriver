using Ricoh.CameraController;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Collections;

namespace ASCOM.PentaxKP
{
    public class PentaxKPCameraEnumerator : PentaxKPCommon
    {
        public ArrayList Cameras
        {
            get
            {
                ArrayList result = new ArrayList();
                Ricoh.CameraController.DeviceInterface deviceInterface = Ricoh.CameraController.DeviceInterface.USB;
                List<CameraDevice> detectedCameraDevices =
                    CameraDeviceDetector.Detect(deviceInterface);
                UInt32 count = (UInt32)detectedCameraDevices.Count(); // GetPortableDeviceCount();
                PortableDeviceInfo portableDeviceInfo = new PortableDeviceInfo();

                //for (UInt32 iter = 0; iter < count; iter++)
                {
                    //hr = GetPortableDeviceInfo(iter, ref portableDeviceInfo);

                    //if (hr == ERROR_SUCCESS)
                    {
                        // Try to open the device
                        //UInt32 handle = OpenDevice(portableDeviceInfo.id);
                        // Sequence contains no elements
                        Response response = detectedCameraDevices.First().Connect(Ricoh.CameraController.DeviceInterface.USB);

                        {
                            DeviceInfo info = new DeviceInfo()
                            {
                                Version = 1
                            };

                            //hr = GetDeviceInfo(handle, ref info);
                            info.DeviceName = detectedCameraDevices.First().Model;
                            info.SerialNumber = detectedCameraDevices.First().SerialNumber;
                            LiveViewSpecification liveViewSpecification = new LiveViewSpecification();
                            detectedCameraDevices.First().GetCameraDeviceSettings(
                                new List<CameraDeviceSetting>() { liveViewSpecification }); ;
                            LiveViewSpecificationValue liveViewSpecificationValue =
                                (LiveViewSpecificationValue)liveViewSpecification.Value;

                            LiveViewImage liveViewImage = liveViewSpecificationValue.Get();
                            info.ImageWidthPixels = liveViewImage.Width;
                            info.ImageHeightPixels = liveViewImage.Height;

                            if (detectedCameraDevices.First().IsConnected(Ricoh.CameraController.DeviceInterface.USB))
                            {
                                result.Add(new PentaxKPCamera(info));
                            }

                            detectedCameraDevices.First().Disconnect(Ricoh.CameraController.DeviceInterface.USB);
                            //CloseDevice(handle);
                        }
                    }
                }

                return result;
            }
        }
    }
}
