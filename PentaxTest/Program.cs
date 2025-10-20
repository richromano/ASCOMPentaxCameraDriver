// This implements a console application that can be used to test an ASCOM driver
//

// This is used to define code in the template that is specific to one class implementation
// unused code can be deleted and this definition removed.

#define Camera
// remove this to bypass the code that uses the chooser to select the driver
//#define UseChooser

using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.Threading;

namespace ASCOM.PentaxKP
{
    class Program
    {
        [MTAThread]
        static void Main(string[] args)
        {
            // Uncomment the code that's required
#if UseChooser
            // choose the device
            string id = ASCOM.DriverAccess.Camera.Choose("");
            if (string.IsNullOrEmpty(id))
                return;
            // create this device
            ASCOM.DriverAccess.Camera device = new ASCOM.DriverAccess.Camera(id);
#else
            // this can be replaced by this code, it avoids the chooser and creates the driver class directly.
            ASCOM.DriverAccess.Camera device = new ASCOM.DriverAccess.Camera("ASCOM.PentaxKP.Camera");
            ASCOM.DriverAccess.Focuser focuser = new ASCOM.DriverAccess.Focuser("ASCOM.PentaxKP.Focuser");
#endif

            device.SetupDialog();
            device.SetupDialog();
            device.Connected = true;
            focuser.Connected = true;
            // now run some tests, adding code to your driver so that the tests will pass.
            // these first tests are common to all drivers.
            Console.WriteLine("name " + device.Name);
            Console.WriteLine("description " + device.Description);
            Console.WriteLine("DriverInfo " + device.DriverInfo);
            Console.WriteLine("driverVersion " + device.DriverVersion);
            //            Console.WriteLine("sensorName " + device.SensorName);

            // TODO add more code to test the driver.
            int count = 0;

//            ArrayList modes = device.ReadoutModes;

            //            device.FastReadout = false;
            device.ReadoutMode = 0;

            Console.WriteLine(device.Gain);
            device.Gain = 0;
            Console.WriteLine(device.Gains.ToString());
            Console.WriteLine(device.MaxADU.ToString());

            for (int j = 0; j < 10; j++)
            {
                int p = 10000;
                for (p = 10000; p > 2000; p = p - 200) {
                    focuser.Move(p);
                    Thread.Sleep(100);
                    Console.Write(".");
                }
                Console.WriteLine(".");
                for (p = 2000; p < 10000; p = p + 200)
                {
                    focuser.Move(p);
                    Thread.Sleep(100);
                    Console.Write(".");
                }
                Console.WriteLine(".");
            }

            for (int j = 0; j < 10; j++)
            {
                int p = 10000;
                for (p = 10000; p > 2000; p = p - 400)
                {
                    focuser.Move(p);
                    Thread.Sleep(100);
                    Console.Write(".");
                }
                Console.WriteLine(".");
                for (p = 2000; p < 10000; p = p + 400)
                {
                    focuser.Move(p);
                    Thread.Sleep(100);
                    Console.Write(".");
                }
                Console.WriteLine(".");
            }

            for (int j = 0; j < 10; j++)
            {
                int p = 10000;
                for (p = 10000; p > 2000; p = p - 800)
                {
                    focuser.Move(p);
                    Thread.Sleep(100);
                    Console.Write(".");
                }
                Console.WriteLine(".");
                for (p = 2000; p < 10000; p = p + 800)
                {
                    focuser.Move(p);
                    Thread.Sleep(100);
                    Console.Write(".");
                }
                Console.WriteLine(".");
            }

            for (int j = 0; j < 10; j++)
            {
                int p = 10000;
                for (p = 10000; p > 2000; p = p - 1600)
                {
                    focuser.Move(p);
                    Thread.Sleep(100);
                    Console.Write(".");
                }
                Console.WriteLine(".");
                for (p = 2000; p < 10000; p = p + 1600)
                {
                    focuser.Move(p);
                    Thread.Sleep(100);
                    Console.Write(".");
                }
                Console.WriteLine(".");
            }

            return;

            for (int j=0;j<1;j++)
            {
                device.StartExposure(3, true);
                Thread.Sleep(1000);
                //                device.AbortExposure();
                for (int i = 0; i < 10000 && !device.ImageReady; i++)
                {
                    Thread.Sleep(250);
                    Console.WriteLine(device.CameraState.ToString());
                }

                object o = device.ImageArray;
                count++;
                Console.WriteLine("Got an image #" + count.ToString());
                GC.Collect();
            }

            device.ReadoutMode = 0;

            Console.WriteLine(device.Gain);
            device.Gain = 0;
            Console.WriteLine(device.Gains.ToString());
            Console.WriteLine(device.MaxADU.ToString());

            for (int j = 0; j < 1; j++)
            {
                device.StartExposure(0.1, true);
                for (int i = 0; i < 100 && !device.ImageReady; i++)
                    Thread.Sleep(250);

                object o = device.ImageArray;
                count++;
                Console.WriteLine("Got an image #" + count.ToString());
                GC.Collect();
            }

            device.ReadoutMode = 0;

            Console.WriteLine(device.Gain);
            device.Gain = 0;
            Console.WriteLine(device.Gains.ToString());
            Console.WriteLine(device.MaxADU.ToString());

            for (int j = 0; j < 1; j++)
            {
                device.StartExposure(0.1, true);
                for (int i = 0; i < 100 && !device.ImageReady; i++)
                    Thread.Sleep(250);

                object o = device.ImageArray;
                count++;
                Console.WriteLine("Got an image #" + count.ToString());
                GC.Collect();
            }

            Console.WriteLine(device.CCDTemperature);
            device.Connected = false;
            Console.WriteLine("Press Enter to finish");
            Console.ReadLine();
        }
    }
}
