using System;
using System.Collections.Generic;
using System.Management;

namespace ASCOMPentaxCameraDriver
{
    public class PTPDeviceInfo
    {
        public string DeviceID { get; set; }
        public string Description { get; set; }
        public string Manufacturer { get; set; }
    }

    public class PTPDeviceEnumerator
    {
        /*private static async Task ListAllCamerasAsync()
        {
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM win32_PNPEntity WHERE PnPClass='Camera'");
            var collection = await Task.Run(() => { return searcher.Get(); });
            var cameraIndex = 0;
            foreach (ManagementObject cameraDevice in collection)
            {
                Console.WriteLine($"Camera {cameraIndex}: {cameraDevice["Name"]}");
                cameraIndex++;
            }
        }*/

        public static List<PTPDeviceInfo> EnumeratePTPDevices()
        {
            var devices = new List<PTPDeviceInfo>();
            //string query = "SELECT * FROM Win32_PnPEntity WHERE Description LIKE '%PTP%' OR Description LIKE '%Picture Transfer Protocol%'";
            string query = "SELECT * FROM win32_PNPEntity WHERE Description LIKE '%PENTAX%'";

            using (var searcher = new ManagementObjectSearcher(query))
            {
                foreach (ManagementObject device in searcher.Get())
                {
                    devices.Add(new PTPDeviceInfo
                    {
                        DeviceID = device["DeviceID"]?.ToString(),
                        Description = device["Description"]?.ToString(),
                        Manufacturer = device["Manufacturer"]?.ToString()
                    });
                }
            }

            return devices;
        }
    }
}