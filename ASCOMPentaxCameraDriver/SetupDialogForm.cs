using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using ASCOM.Utilities;
using ASCOM.PentaxKP;
using System.Collections;

namespace ASCOM.PentaxKP
{
    [ComVisible(false)]					// Form not registered for COM!
    public partial class SetupDialogForm : Form
    {
        internal bool InInit = false;

        public SetupDialogForm()
        {
            InitializeComponent();

            // Initialise current values of user settings from the ASCOM Profile
            InitUI();
        }

        private void cmdOK_Click(object sender, EventArgs e) // OK button event handler
        {
            DriverCommon.Settings.DebugLevel=(int)numericUpDown1.Value;
            // Place any validation constraint checks here
            // Update the state variables with results from the dialogue
            DriverCommon.Settings.DeviceId = (string)comboBoxCamera.SelectedItem;
            //DriverCommon.Settings.DeviceIndex = comboBoxCamera.SelectedIndex;
            DriverCommon.Settings.EnableLogging = chkTrace.Checked;
            // TODO: add NINA check for non-RGGB setting
            DriverCommon.Settings.DefaultReadoutMode = (short)(comboBoxOutputFormat.SelectedIndex);

            //            DriverCommon.Settings.RAWSave = checkBoxEnableSaveLocation.Checked;
            //            DriverCommon.Settings.ARWAutosaveFolder = textBoxSaveLocation.Text;
            //            DriverCommon.Settings.ARWAutosaveWithDate = checkBoxAppendDate.Checked;
            //            DriverCommon.Settings.ARWAutosaveAlwaysCreateEmptyFolder = checkBoxCreateMultipleDirectories.Checked;
            DriverCommon.Settings.UseLiveview = checkBoxUseLiveview.Checked;
            //DriverCommon.Settings.AutoLiveview = checkBoxAutoLiveview.Checked;
            DriverCommon.Settings.Personality = comboBoxPersonality.SelectedIndex;
            DriverCommon.Settings.SerialPort = comboBoxSerialRelay.SelectedIndex+1;
            DriverCommon.Settings.BulbModeEnable = checkBoxBulbMode.Checked;
            //DriverCommon.Settings.BulbModeTime = short.Parse(textBoxBulbMode.Text.Trim());
            DriverCommon.Settings.KeepInterimFiles = checkBoxKeepInterimFiles.Checked;
            //DriverCommon.Settings.UsingCameraLens = checkBoxUsingCameraLens.Checked;
            //DriverCommon.Settings.HandsOffFocus = checkBoxHandsOffFocus.Checked;
        }

        private void cmdCancel_Click(object sender, EventArgs e) // Cancel button event handler
        {
            Close();
        }

        private void BrowseToAscom(object sender, EventArgs e) // Click on ASCOM logo event handler
        {
            try
            {
                System.Diagnostics.Process.Start("http://ascom-standards.org/");
            }
            catch (System.ComponentModel.Win32Exception noBrowser)
            {
                if (noBrowser.ErrorCode == -2147467259)
                    MessageBox.Show(noBrowser.Message);
            }
            catch (System.Exception other)
            {
                MessageBox.Show(other.Message);
            }
        }

        [DllImport("user32.dll")]
        public static extern bool ShowWindowAsync(HandleRef hWnd, int nCmdShow);
        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr WindowHandle);
        public const int SW_RESTORE = 9;

        private void FocusProcess(string procName)
        {
            Process[] objProcesses = System.Diagnostics.Process.GetProcessesByName(procName);
            if (objProcesses.Length > 0)
            {
                IntPtr hWnd = IntPtr.Zero;
                hWnd = objProcesses[0].MainWindowHandle;
                ShowWindowAsync(new HandleRef(null, hWnd), SW_RESTORE);
                SetForegroundWindow(objProcesses[0].MainWindowHandle);
            }
        }

        private void InitUI()
        {
            InInit = true;
            if(DriverCommon.Settings.Personality == PentaxKPProfile.PERSONALITY_NINA)
                FocusProcess(System.Diagnostics.Process.GetCurrentProcess().ProcessName);
            chkTrace.Checked = DriverCommon.Settings.EnableLogging;
            PentaxKPCameraEnumerator enumerator = new PentaxKPCameraEnumerator();
            String selected = "";

            comboBoxCamera.Items.Clear();

            foreach (PentaxKPProfile.DeviceInfo candidate in enumerator.Cameras)
            {
                int id = comboBoxCamera.Items.Add(candidate.DeviceName);

                if (candidate.DeviceName == DriverCommon.Settings.DeviceId)
                {
                    selected = candidate.DeviceName;
                }
            }

            if (selected.Length > 0)
            {
                comboBoxCamera.SelectedItem = selected;
            }

            //checkBoxUsingCameraLens.Checked = DriverCommon.Settings.UsingCameraLens;
            //comboBoxLenses.Enabled = DriverCommon.Settings.UsingCameraLens;
            //buttonFocusTools.Enabled = DriverCommon.Settings.UsingCameraLens;
            //checkBoxHandsOffFocus.Checked = DriverCommon.Settings.HandsOffFocus;
            //checkBoxHandsOffFocus.Enabled = DriverCommon.Settings.UsingCameraLens;

//            checkBoxEnableSaveLocation.Checked = DriverCommon.Settings.RAWSave;
//            textBoxSaveLocation.Enabled = DriverCommon.Settings.RAWSave;
//            textBoxSaveLocation.Text = DriverCommon.Settings.ARWAutosaveFolder;
//            checkBoxAppendDate.Enabled = textBoxSaveLocation.Enabled;
//            checkBoxAppendDate.Checked = DriverCommon.Settings.ARWAutosaveWithDate;
//            checkBoxCreateMultipleDirectories.Enabled = textBoxSaveLocation.Enabled;
//            checkBoxCreateMultipleDirectories.Checked = DriverCommon.Settings.ARWAutosaveAlwaysCreateEmptyFolder;

//            buttonSelectFolder.Enabled = DriverCommon.Settings.RAWSave;
            checkBoxUseLiveview.Checked = DriverCommon.Settings.UseLiveview;
            //checkBoxAutoLiveview.Checked = DriverCommon.Settings.AutoLiveview;

            Dictionary<int, string> personalities = new Dictionary<int, string>();

            //Commenting out makes it crash
            personalities.Add(PentaxKPProfile.PERSONALITY_SHARPCAP, "SharpCap");
            personalities.Add(PentaxKPProfile.PERSONALITY_NINA, "N.I.N.A");

            comboBoxPersonality.DataSource = new BindingSource(personalities, null);
            comboBoxPersonality.DisplayMember = "Value";
            comboBoxPersonality.ValueMember = "Key";

            comboBoxPersonality.SelectedIndex = DriverCommon.Settings.Personality;
            numericUpDown1.Value=DriverCommon.Settings.DebugLevel;

            Dictionary<int, string> comports = new Dictionary<int, string>();

            //Commenting out makes it crash
            comports.Add(1, "COM1");
            comports.Add(2, "COM2");
            comports.Add(3, "COM3");
            comports.Add(4, "COM4");
            comports.Add(5, "COM5");
            comports.Add(6, "COM6");
            comports.Add(7, "COM7");
            comports.Add(8, "COM8");
            comports.Add(9, "COM9");
            comports.Add(10, "COM10");
            comports.Add(11, "COM11");
            comports.Add(12, "COM12");
            comports.Add(13, "COM13");
            comports.Add(14, "COM14");
            comports.Add(15, "COM15");
            comports.Add(16, "COM16");

            comboBoxSerialRelay.DataSource = new BindingSource(comports, null);
            comboBoxSerialRelay.DisplayMember = "Value";
            comboBoxSerialRelay.ValueMember = "Key";
            comboBoxSerialRelay.SelectedIndex = DriverCommon.Settings.SerialPort-1;

            checkBoxBulbMode.Checked = DriverCommon.Settings.BulbModeEnable;
            //textBoxBulbMode.Text = DriverCommon.Settings.BulbModeTime.ToString();
            //textBoxBulbMode.Enabled = checkBoxBulbMode.Checked;

            checkBoxKeepInterimFiles.Checked = DriverCommon.Settings.KeepInterimFiles;

            PopulateOutputFormats();

            comboBoxOutputFormat.SelectedIndex = DriverCommon.Settings.DefaultReadoutMode;

            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            FileVersionInfo fileVersion = FileVersionInfo.GetVersionInfo(assembly.Location);

            //textBoxVersion.Text = fileVersion.FileVersion;

            InInit = false;
//            timer1.Tick += showCameraStatus;
        }

/*        private void showCameraStatus(Object o, EventArgs eventArgs)
        {
            PentaxKPCamera camera = Camera.camera;

            if (camera != null)
            {
                textBoxCameraConnected.Text = camera.Connected ? "Connected" : "Disconnected";

                if (camera.Connected)
                {
                    using (new Camera.SerializedAccess(ascomCamera, "setupDialog"))
                    {
                        Camera.LogMessage("setup", "Refresh Properties");
                        camera.RefreshProperties();
                        Camera.LogMessage("setup", "500e");
                        textBoxCameraMode.Text = camera.GetPropertyValue(0x500e).Text;
                        textBoxCameraCompressionMode.Text = camera.GetPropertyValue(0x5004).Text;
                        textBoxCameraExposureTime.Text = camera.GetPropertyValue(0xd20d).Text;
                        textBoxCameraISO.Text = camera.GetPropertyValue(0xd21e).Text;
                        textBoxCameraBatteryLevel.Text = camera.GetPropertyValue(0xd218).Text;
                        modeWarning.Visible = textBoxCameraMode.Text != "M";
                        Camera.LogMessage("setup", "All Props updated");
                    }
                }
                else
                {
                    textBoxCameraMode.Text = "-";
                    textBoxCameraCompressionMode.Text = "-";
                    textBoxCameraExposureTime.Text = "-";
                    textBoxCameraISO.Text = "-";
                    textBoxCameraBatteryLevel.Text = "-";
                    modeWarning.Visible = false;
                }
            }
            else
            {
                textBoxCameraConnected.Text = "Not Initialized";
            }
        }*/

        private void checkBoxEnableSaveLocation_CheckedChanged(object sender, EventArgs e)
        {
//            textBoxSaveLocation.Enabled = ((CheckBox)sender).Checked;
//            buttonSelectFolder.Enabled = ((CheckBox)sender).Checked;
//            checkBoxAppendDate.Enabled = ((CheckBox)sender).Checked;
//            checkBoxCreateMultipleDirectories.Enabled = ((CheckBox)sender).Checked;
        }

        private void selectFolder_Click(object sender, EventArgs e)
        {
//            selectFolderDialog.SelectedPath = textBoxSaveLocation.Text;

            if (selectFolderDialog.ShowDialog() == DialogResult.OK)
            {
//                textBoxSaveLocation.Text = selectFolderDialog.SelectedPath;
            }
        }

        private void comboBoxPersonality_SelectedIndexChanged(object sender, EventArgs e)
        {
            int personality=PentaxKPProfile.PERSONALITY_SHARPCAP;
            personality = comboBoxPersonality.SelectedIndex;

            if(comboBoxOutputFormat.SelectedIndex!=-1)
                DriverCommon.Settings.DefaultReadoutMode = (short)comboBoxOutputFormat.SelectedIndex;
            
            switch (personality)
            {
                case PentaxKPProfile.PERSONALITY_SHARPCAP:
                    // Sharpcap supports format specification, but wants BGR, not RGB
                    // Doesn't support Liveview selection
                    //comboBoxOutputFormat.Enabled = true;

                    // TODO: Electronic shutter and manual mode

                    //PopulateOutputFormats();

                    // TODO: Fix this so that NINA and Sharpcap are different

                    //DriverCommon.Settings.CurrentOutputFormat = PentaxKPProfile.OUTPUTFORMAT_BGR;

                    //comboBoxOutputFormat.SelectedIndex = DriverCommon.Settings.CurrentOutputFormat;
                    //checkBoxUseLiveview.Enabled = true;
                    //checkBoxUseLiveview.Checked = true;
                    comboBoxOutputFormat.Enabled = true;
                    //checkBoxBulbMode.Checked = false;
                    //checkBoxBulbMode.Enabled = false;
                    break;
                case PentaxKPProfile.PERSONALITY_NINA:
                    // NINA only supports RGGB, so we need to preset format and disable liveview
                    comboBoxOutputFormat.SelectedValue = PentaxKPProfile.OUTPUTFORMAT_RGGB;
                    comboBoxOutputFormat.Enabled = false;
                    //checkBoxBulbMode.Enabled = true;
                    //checkBoxBulbMode.Enabled = false;
                    break;
            }
        }

        private void PopulateOutputFormats()
        {
            Dictionary<short, string> outputFormats = new Dictionary<short, string>();

            outputFormats.Add(PentaxKPProfile.OUTPUTFORMAT_RAWBGR, "RAW/Color (Processed)");
            outputFormats.Add(PentaxKPProfile.OUTPUTFORMAT_BGR, "JPG/Color (Processed)");
            outputFormats.Add(PentaxKPProfile.OUTPUTFORMAT_RGGB, "RAW/RGGB (Unprocessed)");

            /*            switch (comboBoxPersonality.SelectedIndex)
                        {
                            case PentaxKPCommon.PERSONALITY_APT:
                                outputFormats.Add(PentaxKPCommon.OUTPUTFORMAT_RGB, "RGB (Processed)");
                                break;

                            case PentaxKPCommon.PERSONALITY_NINA:
                                break;

                            case PentaxKPProfile.PERSONALITY_SHARPCAP:
                                outputFormats.Add(PentaxKPProfile.OUTPUTFORMAT_BGR, "JPG (Processed)");
                                break;
        }*/

            comboBoxOutputFormat.DataSource = new BindingSource(outputFormats, null);
            comboBoxOutputFormat.DisplayMember = "Value";
            comboBoxOutputFormat.ValueMember = "Key";
        }

       /* private void checkBoxAutoLiveview_CheckedChanged(object sender, EventArgs e)
        {
            if (!InInit && checkBoxAutoLiveview.Checked)
            {
                MessageBox.Show("Please note that this feature is experimental.\n\nThis will automatically take a LiveView image instead of a normal exposure if:\n  - The camera supports it\n  - The exposure time is set to less than\n    or equal to 0.00001s (in APT this is\n    represented as 0.000)");
            }
        }*/

/*        private void textBoxBulbMode_Validating(object sender, CancelEventArgs e)
        {
            // Lowest possible value is 1, highest is 30
            int value = -1;

            try
            {
                value = short.Parse(textBoxBulbMode.Text.Trim());
            }
            catch
            {
                // Value already invalid
            }

            if (value < 1 || value > 30)
            {
                e.Cancel = true;
                MessageBox.Show("Value for Bulb Mode must be a number from 1 to 30");
            }
        }*/

        /*private void checkBoxBulbMode_CheckedChanged(object sender, EventArgs e)
        {
            if (!InInit)
            {
                //textBoxBulbMode.Enabled = checkBoxBulbMode.Checked;
                MessageBox.Show("Note that this option only works if you have a trigger cable.");
            }
        }*/

        /*private void linkWiki_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                // Change the color of the link text by setting LinkVisited
                // to true.
                linkExposureAndISO.LinkVisited = true;

                //Call the Process.Start method to open the default browser
                //with a URL:
                System.Diagnostics.Process.Start("https://github.com/richromano/ASCOMPentaxCameraDriver");
            }
            catch (Exception)
            {
                MessageBox.Show("Unable to open link that was clicked.");
            }
        }*/

       /* private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                // Change the color of the link text by setting LinkVisited
                // to true.
                linkWiki.LinkVisited = true;

                //Call the Process.Start method to open the default browser
                //with a URL:
                System.Diagnostics.Process.Start("https://github.com/richromano/ASCOMPentaxCameraDriver/wiki/");
            }
            catch (Exception)
            {
                MessageBox.Show("Unable to open link that was clicked.");
            }
        }*/

        /*private void checkBoxUsingCameraLens_CheckedChanged(object sender, EventArgs e)
        {
            comboBoxLenses.Enabled = checkBoxUsingCameraLens.Checked;
            checkBoxHandsOffFocus.Enabled = checkBoxUsingCameraLens.Checked;
        }*/

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                // Change the color of the link text by setting LinkVisited
                // to true.
                linkLabel1.LinkVisited = true;

                //Call the Process.Start method to open the default browser
                //with a URL:
                System.Diagnostics.Process.Start("https://nighttime-imaging.eu/docs/master/site/advanced/bulbshutter/");
            }
            catch (Exception)
            {
                MessageBox.Show("Unable to open link that was clicked.");
            }

        }
    }
}