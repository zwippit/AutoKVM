﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Reflection;
using System.Threading;
using System.Runtime.InteropServices;

namespace AutoKVM
{
    public partial class TaskTray : Form
    {
        private List<ToolStripMenuItem> monitorMenus;
        private List<DisplayDDC.MonitorSource[]> supportedMonitorSources;

        private Dictionary<IUSBDevice, List<ToolStripMenuItem>> usbDeviceMenus;
        private Dictionary<HIDAPI.HIDDeviceInfo, ToolStripMenuItem> usbTriggerDevicesMenus;
        private List<HIDAPI.HIDDeviceInfo> checkedUsbTriggerDevices;

        private TriggerHappyService triggerHappyService;
        BackgroundWorker backgroundWorker = null;

        private System.Timers.Timer doublePressTimer;

        public TaskTray()
        {
            InitializeComponent();

            AddMonitorsAndSources();
            AddUSBSwitches();
            AddUSBDevicesForKVMTrigger();
            InitTimer();
            InterceptKeys.RegisterCallback(GlobalKeydownCallback);
            //SetupBackgroundWorkerAndStart();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            this.ShowInTaskbar = false; 
            this.Visible = false;
        }

        private void SetupBackgroundWorkerAndStart()
        {
            backgroundWorker = new BackgroundWorker();
            backgroundWorker.WorkerSupportsCancellation = true;
            backgroundWorker.WorkerReportsProgress = true;
            backgroundWorker.DoWork += BackgroundWorker_DoWork;
            backgroundWorker.ProgressChanged += BackgroundWorker_ProgressChanged;
            

            backgroundWorker.RunWorkerAsync();
        }

        private void BackgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (triggerHappyService.TriggerDisplayCycle)
            {
                triggerHappyService.TriggerDisplayCycle = false;
                CycleDisplays();                
            }
        }

        private void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            triggerHappyService = new TriggerHappyService(checkedUsbTriggerDevices,backgroundWorker);
            triggerHappyService.Start();
        }

        private void AddMonitorsAndSources()
        {
            monitorMenus = new List<ToolStripMenuItem>();
            supportedMonitorSources = DisplayDDC.GetMonitorSupportedSources();
            List<int> activeMonitorSources = DisplayDDC.GetMonitorActiveSources();

            for (int monitorIndex = 0; monitorIndex < supportedMonitorSources.Count; ++monitorIndex)
            {
                ToolStripMenuItem monitor = new ToolStripMenuItem(String.Format("Monitor {0}", monitorIndex + 1));
                monitor.DropDown.Closing += new ToolStripDropDownClosingEventHandler(MonitorDropdownClosing);
                monitorMenus.Add(monitor);
                contextMenuStrip1.Items.Insert(contextMenuStrip1.Items.Count - 1, monitor);

                for (int sourceIndex = 0; sourceIndex < supportedMonitorSources[monitorIndex].Length; ++sourceIndex)
                {
                    DisplayDDC.MonitorSource source = supportedMonitorSources[monitorIndex][sourceIndex];
                    ToolStripMenuItem sourceMenuItem = new ToolStripMenuItem(String.Format("{0}", source.name));
                    sourceMenuItem.CheckOnClick = true;
                    if (source.code == activeMonitorSources[monitorIndex])
                        sourceMenuItem.Checked = true;

                    monitor.DropDownItems.Add(sourceMenuItem);
                }
            }
        }

        private void InitTimer()
        {
            doublePressTimer = new System.Timers.Timer(300);
            doublePressTimer.AutoReset = false;

            if (System.Diagnostics.Debugger.IsAttached)
            {
                doublePressTimer.Interval *= 10; // Add some time, since the debugger can eat some time up.
            }
        }

        private void GlobalKeydownCallback(Keys key)
        {
            switch (key)
            {
                //case Keys.Escape:
                //    Application.Exit();
                //    break;

                case Keys.Pause:
                    HandleScrolllock();
                    break;

                default:
                    break;
            }
        }

        private void HandleScrolllock()
        {
            if (doublePressTimer.Enabled)
            {
                doublePressTimer.Stop();
                CycleDisplays();
                CycleUSBDevicePorts();
            }
            else
            {
                doublePressTimer.Start();
            }
        }

        private void CycleDisplays()
        {
            for (int monitorIndex = 0; monitorIndex < supportedMonitorSources.Count; ++monitorIndex)
            {
                List<int> enabledSources = new List<int>();
                for (int sourceIndex = 0; sourceIndex < supportedMonitorSources[monitorIndex].Length; ++sourceIndex)
                {
                    ToolStripMenuItem sourceMenu = (ToolStripMenuItem)monitorMenus[monitorIndex].DropDownItems[sourceIndex];
                    if (sourceMenu.Checked)
                    {
                        int source = supportedMonitorSources[monitorIndex][sourceIndex].code;
                        enabledSources.Add(source);
                    }
                }

                DisplayDDC.CycleDisplaySources(monitorIndex, enabledSources.ToArray());
            }
        }

        private void notifyIcon1_MouseClick(object sender, MouseEventArgs e)
        {
            if(e.Button.Equals(MouseButtons.Left)) {
                MethodInfo mi = typeof(NotifyIcon).GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic);
                mi.Invoke(notifyIcon1, null);
            }
        }

        private void switchDisplaysToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CycleDisplays();
            CycleUSBDevicePorts();
        }
        
        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void MonitorDropdownClosing(object sender, ToolStripDropDownClosingEventArgs e)
        {
            if (e.CloseReason == ToolStripDropDownCloseReason.ItemClicked)
            {
                e.Cancel = true;
                ((ToolStripDropDownMenu)sender).Invalidate();
            }
        }

        private void AddUSBDevicesForKVMTrigger()
        {
            checkedUsbTriggerDevices = new List<HIDAPI.HIDDeviceInfo>();
            usbTriggerDevicesMenus = new Dictionary<HIDAPI.HIDDeviceInfo, ToolStripMenuItem>();

            ToolStripMenuItem usbTriggerDevicesMenuItem = new ToolStripMenuItem("USB Trigger");
            usbTriggerDevicesMenuItem.DropDown.Closing += new ToolStripDropDownClosingEventHandler(MonitorDropdownClosing);
            contextMenuStrip1.Items.Insert(contextMenuStrip1.Items.Count - 1, usbTriggerDevicesMenuItem);

            List<HIDAPI.HIDDeviceInfo> devices = HIDAPI.HIDEnumerate(0, 0);

            var usbTriggerDevices = devices.OrderBy(o => o.product_string);//.Where(w => w.product_string.ToLower().Contains("trackball") || w.product_string.ToLower().Contains("mouse") || w.product_string.ToLower().Contains("keyboard"));
            foreach (HIDAPI.HIDDeviceInfo usbTriggerDevice in usbTriggerDevices)
            {
                if (usbTriggerDevice.product_string == null)
                    continue;
                //IUSBDevice usbTriggerDeviceInstance = (IUSBDevice)Activator.CreateInstance(usbTriggerDevice);
                if (usbTriggerDevice.product_string.ToLower().Contains("trackball") || usbTriggerDevice.product_string.ToLower().Contains("mouse") || usbTriggerDevice.product_string.ToLower().Contains("keyboard"))
                {
                    ToolStripMenuItem usbTriggerDeviceMenuItem = new ToolStripMenuItem(usbTriggerDevice.product_string);
                    usbTriggerDeviceMenuItem.CheckOnClick = true;
                    usbTriggerDeviceMenuItem.CheckedChanged += UsbTriggerDeviceMenuItem_CheckedChanged;
                    if (!usbTriggerDevicesMenuItem.DropDownItems.ContainsKey(usbTriggerDevice.product_string))
                    {
                        usbTriggerDevicesMenuItem.DropDownItems.Add(usbTriggerDeviceMenuItem);
                        usbTriggerDevicesMenus.Add(usbTriggerDevice, usbTriggerDeviceMenuItem);
                    }
                }
            }
             
        }

        private void UsbTriggerDeviceMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            if (backgroundWorker != null)
            {
                backgroundWorker.CancelAsync();                
            }

            var usbTriggerDeviceMenuItem = (ToolStripMenuItem)sender;
            var triggerDictionaryItem = usbTriggerDevicesMenus.SingleOrDefault(w => w.Value == usbTriggerDeviceMenuItem);            
            if (usbTriggerDeviceMenuItem.Checked)
            {
                checkedUsbTriggerDevices.Add(triggerDictionaryItem.Key);
            }
            else
            {
                if (checkedUsbTriggerDevices.Contains(triggerDictionaryItem.Key))
                {
                    checkedUsbTriggerDevices.Remove(triggerDictionaryItem.Key);
                }
            }
            SetupBackgroundWorkerAndStart();

        }

        private void AddUSBSwitches()
        {
            usbDeviceMenus = new Dictionary<IUSBDevice, List<ToolStripMenuItem>>();

            Type[] supportedDevices = new Type[1] { typeof(ShareCentralIO) };

            ToolStripMenuItem usbSwitchesMenuItem = null;

            List<HIDAPI.HIDDeviceInfo> devices = HIDAPI.HIDEnumerate(0, 0);
            foreach (HIDAPI.HIDDeviceInfo device in devices) {
                int index = Array.FindIndex(supportedDevices, supportedDevice => {
                    DeviceInfoAttribute deviceInfoAttribute = (DeviceInfoAttribute)Attribute.GetCustomAttribute(supportedDevice, typeof(DeviceInfoAttribute));
                    return deviceInfoAttribute.vendorID == device.vendor_id && deviceInfoAttribute.productID == device.product_id;
                });
                
                if (index > -1) {
                    try {
                        IUSBDevice usbSwitch = (IUSBDevice)Activator.CreateInstance(supportedDevices[index]);
                    
                    if (usbSwitchesMenuItem == null) {
                        usbSwitchesMenuItem = new ToolStripMenuItem(String.Format("USB Switches"));
                        contextMenuStrip1.Items.Insert(contextMenuStrip1.Items.Count - 1, usbSwitchesMenuItem);
                    }

                    ToolStripMenuItem usbSwitchMenuItem = new ToolStripMenuItem(String.Format("{0}", device.product_string));
                    usbSwitchMenuItem.DropDown.Closing += new ToolStripDropDownClosingEventHandler(MonitorDropdownClosing);
                    usbSwitchesMenuItem.DropDownItems.Add(usbSwitchMenuItem);

                    
                    List<ToolStripMenuItem> portToolStrips = new List<ToolStripMenuItem>();
                    for (int i = 0; i < usbSwitch.numberOfPorts; ++i) {
                        ToolStripMenuItem usbSwitchPortMenuItem = new ToolStripMenuItem(String.Format("Port {0}", i));
                        usbSwitchPortMenuItem.CheckOnClick = true;
                        usbSwitchPortMenuItem.Checked = true;
                        usbSwitchMenuItem.DropDownItems.Add(usbSwitchPortMenuItem);
                        portToolStrips.Add(usbSwitchPortMenuItem);
                    }

                    usbDeviceMenus.Add(usbSwitch, portToolStrips);

                    //ShareCentralIO.Devices status = usbSwitch.GetStatusOfDevices();
                    //bool device1Status = (status & ShareCentralIO.Devices.Device1) == ShareCentralIO.Devices.Device1;
                    //bool device2Status = (status & ShareCentralIO.Devices.Device2) == ShareCentralIO.Devices.Device2;
                    //bool device3Status = (status & ShareCentralIO.Devices.Device3) == ShareCentralIO.Devices.Device3;
                    //bool device4Status = (status & ShareCentralIO.Devices.Device4) == ShareCentralIO.Devices.Device4;

                    } catch (FailedToOpenDeviceException e) {
                        Console.WriteLine("Failed to open USB Device. {0}", e);
                    }
                }
            }
        }

        private void CycleUSBDevicePorts()
        {
            foreach (KeyValuePair<IUSBDevice, List<ToolStripMenuItem>> usbDevices in usbDeviceMenus) {
                List<int> enabledPorts = new List<int>();
                for (int portIndex = 0; portIndex < usbDevices.Key.numberOfPorts; ++portIndex) {
                    ToolStripMenuItem portMenu = (ToolStripMenuItem)usbDevices.Value[portIndex];
                    if (portMenu.Checked) {
                        enabledPorts.Add(portIndex);
                    }

                    usbDevices.Key.CyclePorts(enabledPorts.ToArray());
                }
            }
        }
    }
}
