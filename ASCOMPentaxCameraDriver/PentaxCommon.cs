using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace ASCOM.PentaxKP
{
    abstract public class PentaxKPCommon
    {
        protected static readonly UInt32 ERROR_SUCCESS = 0;
        protected static readonly UInt32 INVALID_HANDLE_VALUE = 0xffffffff;
        protected const UInt32 STATUS_EXPOSING = 0x01;
        protected const UInt32 STATUS_FAILED = 0x02;
        protected const UInt32 STATUS_CANCELLED = 0x03;
        protected const UInt32 STATUS_COMPLETE = 0x04;
        protected const UInt32 STATUS_STARTING = 0x8001;
        protected const UInt32 STATUS_READING = 0x8002;
        protected const UInt32 FORMAT_ARW = 0xb101;
        protected const UInt32 FORMAT_JPEG = 0x3801;
        protected const UInt32 IMAGEMODE_RAW = 1;
        protected const UInt32 IMAGEMODE_RGB = 2;
        protected const UInt32 IMAGEMODE_JPEG = 3;
        protected const UInt32 INFOFLAG_ACTIVE = 1;

        public const int PERSONALITY_SHARPCAP = 0;

        public const short OUTPUTFORMAT_RGB = (short)IMAGEMODE_RGB;
        public const short OUTPUTFORMAT_BGR = OUTPUTFORMAT_RGB | 0x1000;
        public const short OUTPUTFORMAT_RGGB = (short)IMAGEMODE_RAW;

        public const UInt16 PROPERTY_ISO = 0xd21e;
        public const UInt16 PROPERTY_ISO_OPTIONS = 0xfffe;
        public const UInt16 PROPERTY_FOCUS_CONTROL = 0xd2d1;
        public const UInt16 PROPERTY_BATTERY_TEMPERATURE = 0xfffd;
        public const UInt16 PROPERTY_FOCUS_POSITION = 0xfffc;

        protected const UInt32 CAMERA_SUPPORTS_LIVEVIEW = 0x00000001;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct PortableDeviceInfo
        {
            public string id;
            public string manufacturer;
            public string model;
            public string devicePath;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct DeviceInfo
        {
            public UInt32 Version;
            public UInt32 ImageWidthPixels;
            public UInt32 ImageHeightPixels;
            public UInt32 ImageWidthCroppedPixels;
            public UInt32 ImageHeightCroppedPixels;
            public UInt32 BayerXOffset;
            public UInt32 BayerYOffset;
            public UInt32 CropMode;
            public Double ExposureTimeMin;
            public Double ExposureTimeMax;
            public Double ExposureTimeStep;
            public Double PixelWidth;
            public Double PixelHeight;
            public UInt32 BitsPerPixel;

            public string Manufacturer;
            public string Model;
            public string SerialNumber;
            public string DeviceName;
            public string SensorName;
            public string DeviceVersion;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct ImageInfo
        {
            public UInt32 ImageSize;
            public IntPtr ImageData;
            public UInt32 Status;
            public UInt32 ImageMode;
            public UInt32 Width;
            public UInt32 Height;
            public UInt32 Flags;
            public UInt32 MetaDataSize;
            public IntPtr MetaData;
            public Double ExposureTime;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct CameraInfo
        {
            public UInt32 CameraFlags;
            public UInt32 ImageWidthPixels;
            public UInt32 ImageHeightPixels;
            public UInt32 ImageWidthCroppedPixels;
            public UInt32 ImageHeightCroppedPixels;
            public UInt32 PreviewWidthPixels;
            public UInt32 PreviewHeightPixels;
            public UInt32 BayerXOffset;
            public UInt32 BayerYOffset;
            public Double PixelWidth;
            public Double PixelHeight;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct PropertyValueOption
        {
            public UInt32 Value;
            public string Name;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct PropertyValue
        {
            public UInt32 Id;
            public UInt32 Value;
            public string Text;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct PropertyDescriptor
        {
            public UInt32 Id;
            public UInt16 Type;
            public UInt16 Flags;
            public string Name;
            public UInt32 ValueCount;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct LensInfo
        {
            public string Id;
            public string Manufacturer;
            public string Model;
            public string LensPath;
        }

    }
}
