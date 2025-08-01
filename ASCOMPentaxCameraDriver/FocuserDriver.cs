//tabs=4
// --------------------------------------------------------------------------------
//
// ASCOM Focuser driver for Pentax KP/K1/645Z/K3III Camera
//
// Description:	Implements ASCOM driver for Pentax KP/K1/645Z/K3III Camera.
//
// Implements:	ASCOM Focuser interface version: 3
// Author:		(2025) Richard Romano
//


// This is used to define code in the template that is specific to one class implementation
// unused code canbe deleted and this definition removed.
#define Focuser

using System;
using System.Runtime.InteropServices;

using ASCOM.Astrometry.AstroUtils;
using ASCOM.Utilities;
using ASCOM.DeviceInterface;
using System.Collections;
using System.Threading;
using System.Windows.Forms;

namespace ASCOM.PentaxKP
{
    //
    // Your driver's DeviceID is ASCOM.PentaxKP.Focuser
    //
    // The Guid attribute sets the CLSID for ASCOM.PentaxKP.Focuser
    // The ClassInterface/None addribute prevents an empty interface called
    // _PentaxKPCameraFocuser from being created and used as the [default] interface
    //
    // TODO Replace the not implemented exceptions with code to implement the function or
    // throw the appropriate ASCOM exception.
    //

    /// <summary>
    /// ASCOM Focuser Driver for PentaxKPCameraFocuser.
    /// </summary>
    [Guid("7d5eed52-6e54-4abf-b2db-2ba4c0a8ab45")]
    [ClassInterface(ClassInterfaceType.None)]
    public class Focuser : IFocuserV3
    {
        /// <summary>
        /// Private variable to hold an ASCOM Utilities object
        /// </summary>
        private Util utilities;

        /// <summary>
        /// Private variable to hold an ASCOM AstroUtilities object to provide the Range method
        /// </summary>
        private AstroUtils astroUtilities;

        private bool requestedConnection=false;

        /// <summary>
        /// Initializes a new instance of the <see cref="PentaxKPCameraFocuser"/> class.
        /// Must be public for COM registration.
        /// </summary>
        public Focuser()
        {
            DriverCommon.ReadProfile(); // Read device configuration from the ASCOM Profile store

            DriverCommon.LogFocuserMessage(0,"Focuser", "Starting initialisation");

            utilities = new Util(); //Initialise util object
            astroUtilities = new AstroUtils(); // Initialise astro utilities object
            //TODO: Implement your additional construction here

            DriverCommon.LogFocuserMessage(0,"Focuser", "Completed initialisation");
        }


        //
        // PUBLIC COM INTERFACE IFocuserV3 IMPLEMENTATION
        //

        #region Common properties and methods.

        /// <summary>
        /// Displays the Setup Dialog form.
        /// If the user clicks the OK button to dismiss the form, then
        /// the new settings are saved, otherwise the old values are reloaded.
        /// THIS IS THE ONLY PLACE WHERE SHOWING USER INTERFACE IS ALLOWED!
        /// </summary>
        public void SetupDialog()
        {
            // consider only showing the setup dialog if not connected
            // or call a different dialog if connected
            //            if (IsConnected)
            DialogResult result = MessageBox.Show("Do you want to reset the lens focus limit?", "Confirmation", MessageBoxButtons.OKCancel);

            if (result == DialogResult.OK)
            {
                rezero = 0;
                Move(10000);
                MessageBox.Show("Lens focus limit reset");
                // User clicked OK, handle the action here
            }
            else if (result == DialogResult.Cancel)
            {
                // User clicked Cancel, handle the action here
            }
        }

        public ArrayList SupportedActions
        {
            get
            {
                DriverCommon.LogFocuserMessage(0,"SupportedActions Get", "Returning empty arraylist");
                return new ArrayList();
            }
        }

        public string Action(string actionName, string actionParameters)
        {
            DriverCommon.LogFocuserMessage(0,"", $"Action {actionName}, parameters {actionParameters} not implemented");
            throw new ASCOM.ActionNotImplementedException("Action " + actionName + " is not implemented by this driver");
        }

        public void CommandBlind(string command, bool raw)
        {
            //CheckConnected("CommandBlind");
            // Call CommandString and return as soon as it finishes
//            this.CommandString(command, raw);
            // or
            throw new ASCOM.MethodNotImplementedException("CommandBlind");
            // DO NOT have both these sections!  One or the other
        }

        public bool CommandBool(string command, bool raw)
        {
            //CheckConnected("CommandBool");
//            string ret = CommandString(command, raw);
            // TODO decode the return string and return true or false
            // TODO These commands are used by NINA and could be implemented
            // or
            throw new ASCOM.MethodNotImplementedException("CommandBool");
            // DO NOT have both these sections!  One or the other
        }

        public string CommandString(string command, bool raw)
        {
            //CheckConnected("CommandString");
            // it's a good idea to put all the low level communication with the device here,
            // then all communication calls this function
            // you need something to ensure that only one command is in progress at a time

            throw new ASCOM.MethodNotImplementedException("CommandString");
        }

        public void Dispose()
        {
            utilities.Dispose();
            utilities = null;
            astroUtilities.Dispose();
            astroUtilities = null;
        }

        public bool Connected
        {
            get
            {
                //using (new DriverCommon.SerializedAccess("get_Connected"))
                {
                    DriverCommon.LogFocuserMessage(5,"Connected", "Get {0}", requestedConnection);
                    return requestedConnection;
                }
            }
            set
            {
                DriverCommon.LogFocuserMessage(0,"Connected", "Set {0}", value.ToString());
                requestedConnection = value;
                if (value)
                {
                    rezero = 0;
                    Move(10000);
                }
            }
        }

        public string Description
        {
            get
            {
                return DriverCommon.FocuserDriverDescription;
            }
        }

        public string DriverInfo
        {
            get
            {
                return DriverCommon.FocuserDriverInfo;
            }
        }

        public string DriverVersion
        {
            get
            {
                return DriverCommon.DriverVersion;
            }
        }

        public short InterfaceVersion
        {
            get
            {
                return Convert.ToInt16("3");
            }
        }

        public string Name
        {
            get
            {
                return DriverCommon.FocuserDriverName;
            }
        }

        #endregion

        #region IFocuser Implementation

        // This should be set inside the create
        // TODO: 
        private int focuserPosition = 10000; // Class level variable to hold the current focuser position
        private int rezero = 0;
//        private const int focuserSteps = 10000;

        public bool Absolute
        {
            get
            {
                DriverCommon.LogFocuserMessage(0,"Absolute Get", true.ToString());
                return true;
            }
        }

        public void Halt()
        {
            DriverCommon.LogFocuserMessage(0,"Halt", "Not implemented");
            throw new ASCOM.MethodNotImplementedException("Halt");
        }

        public bool IsMoving
        {
            get
            {
                //using (new DriverCommon.SerializedAccess("get_IsMoving"))
                {
                    DriverCommon.LogFocuserMessage(4,"IsMoving Get", false.ToString());
                    return false; // This focuser always moves instantaneously so no need for IsMoving ever to be True
                }
            }
        }

        public bool Link
        {
            get
            {
                DriverCommon.LogFocuserMessage(0,"Link Get", this.Connected.ToString());
                return requestedConnection;
                //this.Connected; // Direct function to the connected method, the Link method is just here for backwards compatibility
            }
            set
            {
                DriverCommon.LogFocuserMessage(0,"Link Set", value.ToString());
                requestedConnection = value; // this.Connected = value; // Direct function to the connected method, the Link method is just here for backwards compatibility
            }
        }

        public int MaxIncrement
        {
            get
            {
                DriverCommon.LogFocuserMessage(0,"MaxIncrement Get", "200");
                return 200;// DriverCommon.Camera.GetFocusLimit(); // Maximum change in one move
            }
        }

        public int MaxStep
        {
            get
            {
                DriverCommon.LogFocuserMessage(0,"MaxStep Get", "10000");
                return 10000;// DriverCommon.Camera.GetFocusLimit(); // Maximum extent of the focuser, so position range is 0 to 10,000
            }
        }

        public void Move(int Position)
        {
            DriverCommon.LogFocuserMessage(0,"Move", Position.ToString());
            if (rezero<=0)
            {
                focuserPosition = 10000;
                DriverCommon.m_camera.Focus(-10000);
                Thread.Sleep(200);
                DriverCommon.m_camera.Focus(-10000);
                Thread.Sleep(200);
            }

            if (Position > 10000)
                Position = 10000;

            // Check that m_camera is not null 
            DriverCommon.m_camera.Focus(-(Position-focuserPosition));
            focuserPosition = Position;
            if (rezero <= 0)
            {
                Thread.Sleep(200);
                rezero = 10;
            }
            //rezero--;

        }

        public int Position
        {
            get
            {
//                return DriverCommon.Camera.GetFocus();
//                  throw new ASCOM.PropertyNotImplementedException("StepSize", false);
                return focuserPosition; // Return the focuser position
            }
        }

        public double StepSize
        {
            get
            {
                DriverCommon.LogFocuserMessage(0,"StepSize Get", "Not implemented");
                throw new ASCOM.PropertyNotImplementedException("StepSize", false);
            }
        }

        public bool TempComp
        {
            get
            {
                DriverCommon.LogFocuserMessage(4,"TempComp Get", false.ToString());
                return false;
            }
            set
            {
                DriverCommon.LogFocuserMessage(0,"TempComp Set", "Not implemented");
                throw new ASCOM.PropertyNotImplementedException("TempComp", false);
            }
        }

        public bool TempCompAvailable
        {
            get
            {
                DriverCommon.LogFocuserMessage(4,"TempCompAvailable Get", false.ToString());
                return false; // Temperature compensation is not available in this driver
            }
        }

        public double Temperature
        {
            get
            {
                //using (new DriverCommon.SerializedAccess("get_Temperature"))
                {
                    DriverCommon.LogFocuserMessage(4,"Temperature Get", "Not implemented");
                    //                throw new ASCOM.PropertyNotImplementedException("Temperature", false);
                    return 20;
                }
            }
        }

        #endregion

        #region Private properties and methods
        // here are some useful properties and methods that can be used as required
        // to help with driver development

        #region ASCOM Registration

        // Register or unregister driver for ASCOM. This is harmless if already
        // registered or unregistered. 
        //
        /// <summary>
        /// Register or unregister the driver with the ASCOM Platform.
        /// This is harmless if the driver is already registered/unregistered.
        /// </summary>
        /// <param name="bRegister">If <c>true</c>, registers the driver, otherwise unregisters it.</param>
        private static void RegUnregASCOM(bool bRegister)
        {
            // TODO: Put in installer
            using (var P = new ASCOM.Utilities.Profile())
            {
                P.DeviceType = "Focuser";
                if (bRegister)
                {
                    P.Register(DriverCommon.FocuserDriverId, DriverCommon.FocuserDriverDescription);
                }
                else
                {
                    P.Unregister(DriverCommon.FocuserDriverId);
                }
            }
        }

        /// <summary>
        /// This function registers the driver with the ASCOM Chooser and
        /// is called automatically whenever this class is registered for COM Interop.
        /// </summary>
        /// <param name="t">Type of the class being registered, not used.</param>
        /// <remarks>
        /// This method typically runs in two distinct situations:
        /// <list type="numbered">
        /// <item>
        /// In Visual Studio, when the project is successfully built.
        /// For this to work correctly, the option <c>Register for COM Interop</c>
        /// must be enabled in the project settings.
        /// </item>
        /// <item>During setup, when the installer registers the assembly for COM Interop.</item>
        /// </list>
        /// This technique should mean that it is never necessary to manually register a driver with ASCOM.
        /// </remarks>
        [ComRegisterFunction]
        public static void RegisterASCOM(Type t)
        {
            RegUnregASCOM(true);
        }

        /// <summary>
        /// This function unregisters the driver from the ASCOM Chooser and
        /// is called automatically whenever this class is unregistered from COM Interop.
        /// </summary>
        /// <param name="t">Type of the class being registered, not used.</param>
        /// <remarks>
        /// This method typically runs in two distinct situations:
        /// <list type="numbered">
        /// <item>
        /// In Visual Studio, when the project is cleaned or prior to rebuilding.
        /// For this to work correctly, the option <c>Register for COM Interop</c>
        /// must be enabled in the project settings.
        /// </item>
        /// <item>During uninstall, when the installer unregisters the assembly from COM Interop.</item>
        /// </list>
        /// This technique should mean that it is never necessary to manually unregister a driver from ASCOM.
        /// </remarks>
        [ComUnregisterFunction]
        public static void UnregisterASCOM(Type t)
        {
            RegUnregASCOM(false);
        }

        #endregion

        /// <summary>
        /// Returns true if there is a valid connection to the driver hardware
        /// </summary>
        private bool IsConnected
        {
            get
            {
                return DriverCommon.FocuserConnected;
            }
        }

        /// <summary>
        /// Use this function to throw an exception if we aren't connected to the hardware
        /// </summary>
        /// <param name="message"></param>
        private void CheckConnected(string message)
        {
            if (!IsConnected)
            {
                throw new ASCOM.NotConnectedException(message);
            }
        }

        #endregion
    }
}
