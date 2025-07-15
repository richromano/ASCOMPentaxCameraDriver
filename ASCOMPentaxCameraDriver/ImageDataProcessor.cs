using ASCOM.PentaxKP.Native;
using System;
using System.Runtime.InteropServices;

namespace ASCOM.PentaxKP.Classes
{
    public class ImageDataProcessor
    {

        private IntPtr LoadRaw(string fileName)
        {
            IntPtr data = NativeMethods.libraw_init(LibRaw_constructor_flags.LIBRAW_OPIONS_NO_DATAERR_CALLBACK);
            CheckError(NativeMethods.libraw_open_file(data, fileName), "open file");
            CheckError(NativeMethods.libraw_unpack(data), "unpack");
            CheckError(NativeMethods.libraw_raw2image(data), "raw2image");
            // Don't subtract black level as that pushes the histogram right down to the left hand side for dark areas - ie data being lost
            //CheckError(NativeMethods.libraw_subtract_black(data), "subtract");

            return data;
        }

        private void CheckError(int errorCode, string action)
        {
            if (errorCode != 0)
                throw new Exception($"LibRaw returned error code {errorCode} when {action}");
        }
        

        public int[,,] ReadRawPentax(string fileName)
        {
            DriverCommon.LogCameraMessage(1,"ReadRawPentax", "in");
            IntPtr data = LoadRaw(fileName);
            DriverCommon.LogCameraMessage(1, "ReadRawPentax", "loadraw");
            NativeMethods.libraw_dcraw_process(data);
            DriverCommon.LogCameraMessage(1, "ReadRawPentax", "dcraw_process");

            var dataStructure = GetStructure<libraw_data_t>(data);
            ushort width = dataStructure.sizes.iwidth;
            ushort height = dataStructure.sizes.iheight;

            var pixels = new int[width, height, 3];

            for(int rc=0; rc < width * height; rc++)
            {
                var r = (ushort)Marshal.ReadInt16(dataStructure.image, rc * 8);
                var g = (ushort)Marshal.ReadInt16(dataStructure.image, rc * 8 + 2);
                var b = (ushort)Marshal.ReadInt16(dataStructure.image, rc * 8 + 4);

                int row = rc / width;
                int col = rc - width * row;
                //int rowReversed = height - row - 1;
                pixels[col, row, 0] = b;
                pixels[col, row, 1] = g;
                pixels[col, row, 2] = r;
            };

            DriverCommon.LogCameraMessage(1, "ReadRawPentax", "out");

            NativeMethods.libraw_close(data);

            return pixels;
        }

        public int[,] ReadRBBGPentax(string fileName)
        {
            IntPtr data = LoadRaw(fileName);

            var dataStructure = GetStructure<libraw_data_t>(data);

            var colorsStr = dataStructure.idata.cdesc;

            if (colorsStr != "RGBG")
                throw new NotImplementedException();

            int xoffs = 0;
            int yoffs = 0;

            string cameraPattern = "";
            cameraPattern += colorsStr[NativeMethods.libraw_COLOR(data, 0, 0)];
            cameraPattern += colorsStr[NativeMethods.libraw_COLOR(data, 0, 1)];
            cameraPattern += colorsStr[NativeMethods.libraw_COLOR(data, 1, 0)];
            cameraPattern += colorsStr[NativeMethods.libraw_COLOR(data, 1, 1)];

            switch (cameraPattern)
            {
                case "RGGB":
                    break;
/*                case "GRBG":
                    xoffs = 1;
                    break;
                case "BGGR":
                    xoffs = 1;
                    yoffs = 1;
                    break;
                case "GBRG":
                    yoffs = 1;
                    break;*/
                default:
                    throw new System.NotImplementedException();
            }

            ushort width = dataStructure.sizes.iwidth;
            ushort height = dataStructure.sizes.iheight;

            var pixels = new int[width, height];

            // for now only handle RGGB
            for (int rc = 0; rc < width * height; rc++)
            {
                // Copy out all the values
                var r = (ushort)Marshal.ReadInt16(dataStructure.image, rc * 8);
                var g = (ushort)Marshal.ReadInt16(dataStructure.image, rc * 8 + 2);
                var b = (ushort)Marshal.ReadInt16(dataStructure.image, rc * 8 + 4);

                int row = rc / width;
                int col = rc - width * row;

                if (row % 2 == 0 && col % 2 == 0)
                    // Red
                    pixels[col, row] = r;
                if (row % 2 == 0 && col % 2 == 1)
                    // Green
                    pixels[col, row] = g;
                if (row % 2 == 1 && col % 2 == 0)
                    // Green
                    pixels[col, row] = g;
                if (row % 2 == 1 && col % 2 == 1)
                    // Blue
                    pixels[col, row] = b;
            };

/*            for (int y=0; y<height - yoffs; y++)
            {

                int i0 = NativeMethods.libraw_COLOR(data, y, 0);
                int i1 = NativeMethods.libraw_COLOR(data, y, 1);
                int index = width * 8 * y;
//                var v = (ushort)Marshal.ReadInt16(dataStructure.image, width * 8 * y);
//                ushort* ptr = (ushort*)((byte*)dataStructure.image.ToPointer() + width * 8 * y);

                for (int x = 0; x < width - xoffs; x += 2)
                {
                    pixels[x + xoffs, y + yoffs] = (ushort)Marshal.ReadInt16(dataStructure.image, index+i0);
                    index += 2;
                    pixels[x + xoffs + 1, y + yoffs] = (ushort)Marshal.ReadInt16(dataStructure.image, index+i1);
                    index += 2;
                }

            }*/

/*            if (dataStructure.color.maximum > 0)
            {
                int multiplier = (int)Math.Pow(2, Math.Floor(Math.Log(32768.0 / dataStructure.color.maximum, 2)));
                if (multiplier > 1)
                {

                    for(int y=0;y<height;y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            pixels[x, y] *= multiplier;
                        }
                    }

                }
            }*/
            NativeMethods.libraw_close(data);

            return pixels;
        }

        private T GetStructure<T>(IntPtr ptr)
        where T : struct
        {
            return (T)Marshal.PtrToStructure(ptr, typeof(T));
        }
               
    }
}
