using Honeywell.DatamaxPlusSdk;
using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace HF1860
{
    public partial class Form1 : Form
    {
        private readonly SdkHelper _sdkHelper;
        private bool _isConnected = false;
        private int passCount = 0;
        private int failCount = 0;

        public Form1()
        {
            InitializeComponent();
            _sdkHelper = new SdkHelper();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            InitializeSdk();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            CleanupSdk();
        }

        private void InitializeSdk()
        {
            SDK_RET_E result = _sdkHelper.init();
            if (result == SDK_RET_E.RET_OK)
            {
                Console.WriteLine("SDK Initialized Successfully.");
            }
            else
            {
                MessageBox.Show("SDK Initialization Failed", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CleanupSdk()
        {
            _sdkHelper.deInit();
            Console.WriteLine("SDK resources cleared.");
        }

        private void btnSearch_Click(object sender, EventArgs e)
        {
            SearchDevices();
        }

        private void SearchDevices()
        {
            _sdkHelper.registerCbFoundDeviceNwk(DeviceFoundCallback);
            SDK_RET_E result = _sdkHelper.searchDevices();

            if (result == SDK_RET_E.RET_OK)
            {
                //lblStatus.Text = "Searching for devices...";
                Console.WriteLine("Searching for devices...");
            }
            else
            {
                MessageBox.Show("Device search failed!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DeviceFoundCallback(NwkDeviceParam device)
        {
            // Update the ComboBox on the UI thread
            if (cmbodevice.InvokeRequired)
            {
                cmbodevice.Invoke(new Action(() => cmbodevice.Items.Add(device.mStrSN)));
            }
            else
            {
                cmbodevice.Items.Add(device.mStrSN);
            }
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            if (_isConnected)
            {
                DisconnectDevice();
            }
            else
            {
                ConnectDevice();
            }
        }

        private void ConnectDevice()
        {
            string deviceSN = cmbodevice.Text;

            if (string.IsNullOrEmpty(deviceSN))
            {
                MessageBox.Show("Please select a device from the list.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SDK_RET_E result = _sdkHelper.connectDevice(deviceSN);
            if (result == SDK_RET_E.RET_OK)
            {
                Console.WriteLine("Device connected successfully.");
                _isConnected = true;
                UpdateConnectButton(true);
                StartLiveView();
                RegisterDecodeCallback();
                GetAndDisplayOutputDelayTime();
            }
            else
            {
                MessageBox.Show("Failed to connect to device.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DisconnectDevice()
        {
            string deviceSN = cmbodevice.Text;

            if (string.IsNullOrEmpty(deviceSN))
            {
                MessageBox.Show("Please select a device from the list.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SDK_RET_E result = _sdkHelper.disconnectDevice(deviceSN);
            if (result == SDK_RET_E.RET_OK)
            {
                Console.WriteLine("Device disconnected successfully.");
                _isConnected = false;
                UpdateConnectButton(false);
                StopLiveView();
            }
            else
            {
                MessageBox.Show("Failed to disconnect device.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateConnectButton(bool isConnected)
        {
            btnConnect.Text = isConnected ? "Disconnect" : "Connect";
            btnConnect.BackColor = isConnected ? Color.Red : Color.Green;
        }

        private void StartLiveView()
        {
            RegisterImageCallback();
            ToggleLiveView(true); // Enable live view
        }

        private void StopLiveView()
        {
            ToggleLiveView(false); // Disable live view
        }
        private void RegisterImageCallback()
        {
            string deviceSN1 = cmbodevice.Text;
            _sdkHelper.registerCbRecvImage((imageData, deviceSN) =>
            {
                if (imageData.image != IntPtr.Zero && imageData.imageSize > 0)
                {
                    // Marshal the image data from unmanaged memory to a managed byte array
                    byte[] managedImageData = new byte[imageData.imageSize];
                    Marshal.Copy(imageData.image, managedImageData, 0, imageData.imageSize);

                    // Convert the byte array to a Bitmap
                    using (var ms = new MemoryStream(managedImageData))
                    {
                        try
                        {
                            var bitmap = new Bitmap(ms);

                            // Update the PictureBox on the UI thread
                            if (pictureBox1.InvokeRequired)
                            {
                                pictureBox1.Invoke(new Action(() => pictureBox1.Image = bitmap));
                            }
                            else
                            {
                                pictureBox1.Image = bitmap;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to convert image data to bitmap: {ex.Message}");
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Received image data is null or invalid.");
                }
            }, cmbodevice.Text);
        }
        private void ToggleLiveView(bool turnOn)
        {
            string deviceSN = cmbodevice.Text;

            if (string.IsNullOrEmpty(deviceSN))
            {
                MessageBox.Show("Please select a device from the list.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Thread.Sleep(1000);
            SDK_RET_E result = _sdkHelper.setLiveViewOn(turnOn, deviceSN);

            if (result == SDK_RET_E.RET_OK)
            {
                Console.WriteLine($"Live view {(turnOn ? "enabled" : "disabled")} successfully.");
            }
            else
            {
                // MessageBox.Show($"Failed to {(turnOn ? "enable" : "disable")} live view.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void RegisterDecodeCallback()
        {
            string deviceSN1 = cmbodevice.Text;

            _sdkHelper.registerCbRecvDecode((string code, int len, string sSn) =>
            {
                // Determine the status based on the length of the decoded code
                string status = len > 20 ? "PASS" : "FAIL";

                // Update the UI using a single method
                UpdateUI(code, status);

            }, deviceSN1);
        }
        private void UpdateUI(string code, string status)
        {
            // Ensure this method is called on the UI thread
            if (InvokeRequired)
            {
                Invoke(new Action(() => UpdateUI(code, status)));
            }
            else
            {
                // Append the decoded code to the TextBox
                textBoxOutput.AppendText(code + Environment.NewLine);

                // Update the status label and its color based on the status
                lblstatus.Text = status;
                lblstatus.ForeColor = status == "PASS" ? Color.Green : Color.Red;

                // Update the counts based on the status
                if (status == "PASS")
                {
                    passCount++;
                }
                else
                {
                    failCount++;
                }

                // Update the labels showing the count of passes and fails
                labelPassCount.Text = passCount.ToString();
                labelFailCount.Text = failCount.ToString();
            }
        }
        private void SetOutputDelay()
        {
            int jobId = 0;          // Example job ID
            int pinIndex = 1;       // Example pin index
            int outputDelay = Convert.ToInt32(txtdelay.Text); // Set the output delay time in milliseconds (0-5000 ms)
            string deviceSN = cmbodevice.Text;  // Get the device serial number from the ComboBox

            // Call the wrapper method to set the output delay time
            SDK_RET_E result = _sdkHelper.setOutputDelayTime(jobId, pinIndex, outputDelay, deviceSN);

            if (result == SDK_RET_E.RET_OK)
            {
                MessageBox.Show($"Output Delay Time set to {outputDelay} ms successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("Failed to set output delay time.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void GetAndDisplayOutputDelayTime()
        {
            int jobId = 0;          // Example job ID
            int pinIndex = 1;       // Example pin index
            int outputDelay = 0;    // Variable to store the output delay time
            string deviceSN = cmbodevice.Text;  // Get the device serial number from the ComboBox

            // Call the wrapper method
            SDK_RET_E result = _sdkHelper.getOutputDelayTime(jobId, pinIndex, ref outputDelay, deviceSN);

            if (result == SDK_RET_E.RET_OK)
            {
                txtdelay.Text = outputDelay.ToString();
            }
            else
            {
                MessageBox.Show("Failed to get output delay time.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DisplayOutputEvents()
        {
            int jobId = 0;           // Example job ID
            int pinIndex = 1;        // Example pin index
            int eventCount = 10;     // Initial size of the output event array
            OutputEvent_E[] outputEvents = new OutputEvent_E[eventCount];
            string deviceSN = cmbodevice.Text;  // Get the device serial number from the ComboBox

            // Call the wrapper method
            SDK_RET_E result = _sdkHelper.getOutputEvent(jobId, pinIndex, outputEvents, ref eventCount, deviceSN);

            if (result == SDK_RET_E.RET_OK)
            {
                // Resize the array to match the actual number of events returned
                Array.Resize(ref outputEvents, eventCount);

                string message = "Output Events:\n" + string.Join("\n", outputEvents);
                MessageBox.Show(message, "Output Events", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("Failed to get output events.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            //DisplayOutputEvents();
            SetOutputDelay();
        }
    }
}
