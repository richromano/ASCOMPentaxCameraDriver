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
                UInt32 count = (UInt32)detectedCameraDevices.Count();

                foreach (CameraDevice camera in detectedCameraDevices)
                {
                        // Try to open the device
                        // Sequence contains no elements
                        Response response = camera.Connect(Ricoh.CameraController.DeviceInterface.USB);

                        DeviceInfo info = new DeviceInfo()
                        {
                            Version = 1
                        };

                        info.DeviceName = camera.Model;
                        info.SerialNumber = camera.SerialNumber;
                        LiveViewSpecification liveViewSpecification = new LiveViewSpecification();
                        camera.GetCameraDeviceSettings(
                            new List<CameraDeviceSetting>() { liveViewSpecification }); ;
                        LiveViewSpecificationValue liveViewSpecificationValue =
                            (LiveViewSpecificationValue)liveViewSpecification.Value;

                        LiveViewImage liveViewImage = liveViewSpecificationValue.Get();
                        info.ImageWidthPixels = liveViewImage.Width;
                        info.ImageHeightPixels = liveViewImage.Height;

                        if (camera.IsConnected(Ricoh.CameraController.DeviceInterface.USB))
                        {
                            result.Add(new PentaxKPCamera(info));
                        }

                        camera.Disconnect(Ricoh.CameraController.DeviceInterface.USB);
                }

                return result;
            }
        }
    }
}
