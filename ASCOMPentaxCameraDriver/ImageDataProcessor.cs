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
            IntPtr data = LoadRaw(fileName);
            NativeMethods.libraw_dcraw_process(data);

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
