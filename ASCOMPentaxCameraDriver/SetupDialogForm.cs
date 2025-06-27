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
            // Place any validation constraint checks here
            // Update the state variables with results from the dialogue
            DriverCommon.Settings.DeviceId = (string)comboBoxCamera.SelectedItem;
            DriverCommon.Settings.EnableLogging = chkTrace.Checked;
            DriverCommon.Settings.DefaultReadoutMode = (short)(comboBoxOutputFormat.SelectedIndex);
            if (DriverCommon.Settings.DefaultReadoutMode == 0)
                DriverCommon.Settings.RAWSave = true;
            else
                DriverCommon.Settings.RAWSave = false;

            //            DriverCommon.Settings.RAWSave = checkBoxEnableSaveLocation.Checked;
            //            DriverCommon.Settings.ARWAutosaveFolder = textBoxSaveLocation.Text;
            //            DriverCommon.Settings.ARWAutosaveWithDate = checkBoxAppendDate.Checked;
            //            DriverCommon.Settings.ARWAutosaveAlwaysCreateEmptyFolder = checkBoxCreateMultipleDirectories.Checked;
            DriverCommon.Settings.UseLiveview = checkBoxUseLiveview.Checked;
            //DriverCommon.Settings.AutoLiveview = checkBoxAutoLiveview.Checked;
            DriverCommon.Settings.Personality = comboBoxPersonality.SelectedIndex+1;
            //DriverCommon.Settings.BulbModeEnable = checkBoxBulbMode.Checked;
            //DriverCommon.Settings.BulbModeTime = short.Parse(textBoxBulbMode.Text.Trim());
            //DriverCommon.Settings.AllowISOAdjust = checkBoxAllowISOAdjust.Checked;
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

        private void InitUI()
        {
            InInit = true;
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
//            personalities.Add(PentaxKPCommon.PERSONALITY_APT, "APT");
//            personalities.Add(PentaxKPCommon.PERSONALITY_NINA, "N.I.N.A");
            personalities.Add(PentaxKPProfile.PERSONALITY_SHARPCAP, "SharpCap");

            comboBoxPersonality.DataSource = new BindingSource(personalities, null);
            comboBoxPersonality.DisplayMember = "Value";
            comboBoxPersonality.ValueMember = "Key";

            comboBoxPersonality.SelectedIndex = 0;// DriverCommon.Settings.Personality;

            //checkBoxBulbMode.Checked = DriverCommon.Settings.BulbModeEnable;
            //textBoxBulbMode.Text = DriverCommon.Settings.BulbModeTime.ToString();
            //textBoxBulbMode.Enabled = checkBoxBulbMode.Checked;

            //checkBoxAllowISOAdjust.Checked = DriverCommon.Settings.AllowISOAdjust;

            PopulateOutputFormats();

            comboBoxOutputFormat.SelectedIndex = 0;// DriverCommon.Settings.DefaultReadoutMode;

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
            personality = comboBoxPersonality.SelectedIndex+1;

            short currentOutputFormat = (short)comboBoxOutputFormat.SelectedIndex;//.SelectedValue != null ? (short)comboBoxOutputFormat.SelectedValue : PentaxKPCommon.OUTPUTFORMAT_RGGB;

            switch (personality)
            {
                case PentaxKPProfile.PERSONALITY_SHARPCAP:
                    // Sharpcap supports format specification, but wants BGR, not RGB
                    // Doesn't support Liveview selection
                    comboBoxOutputFormat.Enabled = true;

                    PopulateOutputFormats();

                    if (currentOutputFormat == PentaxKPProfile.OUTPUTFORMAT_RGB)
                    {
                        currentOutputFormat = PentaxKPProfile.OUTPUTFORMAT_BGR;
                    }

                    comboBoxOutputFormat.SelectedValue = currentOutputFormat;
                    checkBoxUseLiveview.Enabled = true;
                    checkBoxUseLiveview.Checked = true;
                    break;
            }
        }

        private void PopulateOutputFormats()
        {
            Dictionary<short, string> outputFormats = new Dictionary<short, string>();

            outputFormats.Add(PentaxKPProfile.OUTPUTFORMAT_RGGB, "RAW/RGGB (Unprocessed)");

            switch (comboBoxPersonality.SelectedIndex)
            {
/*                case PentaxKPCommon.PERSONALITY_APT:
                    outputFormats.Add(PentaxKPCommon.OUTPUTFORMAT_RGB, "RGB (Processed)");
                    break;

                case PentaxKPCommon.PERSONALITY_NINA:
                    break;
*/
                case PentaxKPProfile.PERSONALITY_SHARPCAP:
                    outputFormats.Add(PentaxKPProfile.OUTPUTFORMAT_BGR, "JPG (Processed)");
                    break;
            }

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

/*        private void checkBoxBulbMode_CheckedChanged(object sender, EventArgs e)
        {
            if (!InInit)
            {
                textBoxBulbMode.Enabled = checkBoxBulbMode.Checked;
                MessageBox.Show("Note that this option will only take effect if your camera's list of supported Exposure times is known.  See the wiki page at the bottom of the settings page for more info.");
            }
        }*/

        private void checkBoxAllowISOAdjust_CheckedChanged(object sender, EventArgs e)
        {
            if (!InInit)
            {
                MessageBox.Show("Note that this option will only take effect if your camera's list of supported ISO values is known.  See the wiki page at the bottom of the settings page for more info.");
            }
        }

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

        private void button1_Click(object sender, EventArgs e)
        {
//            using (FocusTools F = new FocusTools())
            {
//                var result = F.ShowDialog();
//                if (result == System.Windows.Forms.DialogResult.OK)
//                {
//                }
            }
        }

        private void checkBoxUseLiveview_CheckedChanged(object sender, EventArgs e)
        {

        }
    }
}