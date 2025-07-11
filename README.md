# ASCOMPentaxKPCameraDriver

ASCOM Camera and Focuser Driver for Pentax KP, K1, K1ii, 645z cameras.

This is based on the Ricoh Camera SDK which supports the following cameras:
https://ricohapi.github.io/docs/camera-usb-sdk-dotnet/

Newer Pentax cameras might also be supported: KF and K70 but not K3iii.

Developed by Richard Romano

# Usage

The driver has only been tested with a KP camera with Sharpcap and N.I.N.A.  You can select LiveView in the Read Mode and then use the LiveView button in Sharpcap.  This will allow fast image streaming with which you can perform initial focus adjustment.

# Testing

There is an issue changing Read Modes in Sharpcap and Sharpcap is currently being updated.  After you change the Read Mode you must also update the Capture Area (ROI).  In N.I.N.A. you can use the Fast Capture mode. 

# Building and Installing

Build the solution in VS2015.  The dlls will automatically be registered.  This software is designed and tested in 64bit on Windows 11.

# Dependencies

The driver uses the Ricoh Camera SDK USB for .NET and uses LibRaw.

# Thanks

Special thanks to Doug Henderson the developer of the Sony Mirrorless Driver on which this work is based. 
