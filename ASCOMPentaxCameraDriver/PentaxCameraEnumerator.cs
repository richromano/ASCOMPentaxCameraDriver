using Ricoh.CameraController;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Collections;
using ASCOMPentaxCameraDriver;

namespace ASCOM.PentaxKP
{
    public class PentaxKPCameraEnumerator
    {
        public ArrayList Cameras
        {
            get
            {
                ArrayList result = new ArrayList();
                Ricoh.CameraController.DeviceInterface deviceInterface = Ricoh.CameraController.DeviceInterface.USB;
                List<CameraDevice> detectedCameraDevices =
                    CameraDeviceDetector.Detect(deviceInterface);

                var devices = PTPDeviceEnumerator.EnumeratePTPDevices();

                foreach (PTPDeviceInfo device in devices)
                {
                    PentaxKPProfile.DeviceInfo info = new PentaxKPProfile.DeviceInfo();
                    info.DeviceName = device.Description;
                    info.SerialNumber = device.DeviceID;
                    result.Add(info);
                }

/*                UInt32 count = (UInt32)detectedCameraDevices.Count();

                foreach (CameraDevice camera in detectedCameraDevices)
                {
                        // Try to open the device
                        // Sequence contains no elements
                        Response response = camera.Connect(Ricoh.CameraController.DeviceInterface.USB);

                        PentaxKPProfile.DeviceInfo info = new PentaxKPProfile.DeviceInfo()
                        {
                            Version = 1
                        };

                        info.DeviceName = camera.Model;
                        info.SerialNumber = camera.SerialNumber;

                        {
                            result.Add(info);
                        }

                        camera.Disconnect(Ricoh.CameraController.DeviceInterface.USB);
                }*/

                return result;
            }
        }
    }
}
