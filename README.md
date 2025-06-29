# ASCOMPentaxKPCameraDriver

ASCOM Camera and Focuser Driver for Pentax KP Camera.

This is based on the Ricoh Camera SDK which supports the following cameras:
https://ricohapi.github.io/docs/camera-usb-sdk-dotnet/

Developed by Richard Romano

# Usage

The driver has only been tested with a KP camera with Sharpcap.  It currently assumes a resolution of 6016x4000 for full sensor and 720x480 for LiveView.  You can select LiveView in the Read Mode and then using the LiveView button in Sharpcap.  This will allow fast image streaming with which you can perform initial focus adjustment.

# Testing

The software has only been tested with Sharpcap.  There is an issue changing Read Modes and Sharpcap is currently being updated.  After you change the Read Mode you must also update the Capture Area.

# Building and Installing

Build the solution in VS2015.  The dlls will automatically be registered.  This software is designed and tested in 64bit on Windows 11.

# Dependencies

The driver uses the Ricoh Camera SDK USB for .NET and uses LibRaw.

# Thanks

Special thanks to Doug Henderson the developer of the Sony Mirrorless Driver on which this work is based
