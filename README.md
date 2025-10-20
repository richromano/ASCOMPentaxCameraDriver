# ASCOMPentaxKPCameraDriver

ASCOM Camera and Focuser Driver for Pentax KP, K1, K1ii, K3iii, 645z cameras.

This is based on the Ricoh Camera SDK which supports the following cameras:
https://ricohapi.github.io/docs/camera-usb-sdk-dotnet/

Currently KF and K70 Pentax cameras are supported but without live view or the focuser.

Developed by Richard Romano

# Read the Manual

https://github.com/richromano/ASCOMPentaxCameraDriver/blob/main/Pentax%20KP%20ASCOM%20Driver%20User%20Manual.pdf

# Usage

Turn on the camera and set the mode to Manual or Bulb.  Make sure the USB mode is set to PTP or MTP.  Set the shutter to Electronic Shutter if desired.  By default LiveView is always on.  This combination will eliminate any mirror slap.  Plug your camera into the USB on your computer.  Your camera should show up as a Camera in Windows Explorer not as a hard drive (it should not have a drive letter).

# Focuser

Be sure to also select Pentax KP/K1 in the Focuser and connect.  This will allow Sharpcap or N.I.N.A. to perform autofocus using autofocus Pentax lens or the 1.7x autofocus teleconverter.

# Dependencies

The driver uses the Ricoh Camera SDK USB for .NET and uses LibRaw.

# Thanks

Special thanks to Doug Henderson the developer of the Sony Mirrorless Driver on which this work is based. 
