# ASCOMPentaxKPCameraDriver

ASCOM Camera and Focuser Driver for Pentax KP, K1, K1ii, 645z cameras.

This is based on the Ricoh Camera SDK which supports the following cameras:
https://ricohapi.github.io/docs/camera-usb-sdk-dotnet/

Newer Pentax cameras might also be supported: KF and K70 but not K3iii.

Developed by Richard Romano

# Usage

Turn on the camera and set the mode to Manual.  Make sure the USB mode is set to PTP or MTP.  Set the shutter to Electronic Shutter if desired.  By default LiveView is always on.  This combination will eliminate any mirror slap.  Plug your camera into the USB on your computer.  Your camera should show up as a Camera in Windows Explorer not as a hard drive (it should not have a drive letter).

The driver has only been tested with a KP camera with Sharpcap and N.I.N.A.  You can select LiveView in the Read Mode and then use the LiveView button in Sharpcap.  In N.I.N.A. you can use the Fast Capture mode to switch modes. This will allow fast image streaming (at 720x480) with which you can perform initial focus adjustment.

# Focuser

Be sure to also select Pentax KP/K1 in the Focuser and connect.  This will allow Sharpcap or N.I.N.A. to perform autofocus.

# Testing

There is an issue changing Read Modes in Sharpcap and Sharpcap is currently being updated.  After you change the Read Mode you must also update the Capture Area (ROI).  

# Building and Installing

Build the solution in VS2015.  The dlls will automatically be registered.  This software is designed and tested in 64bit on Windows 11.  If you use a prebuilt release please the ZIP file in a long term location, unzip the file and run the install shortcut.

# Dependencies

The driver uses the Ricoh Camera SDK USB for .NET and uses LibRaw.

# Thanks

Special thanks to Doug Henderson the developer of the Sony Mirrorless Driver on which this work is based. 
