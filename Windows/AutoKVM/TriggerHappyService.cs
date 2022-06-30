using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;

namespace AutoKVM
{
    public class TriggerHappyService : IDisposable
    {
        private bool disposedValue;
        private BackgroundWorker _backgroundWorker;
       

        public TriggerHappyService(List<HIDAPI.HIDDeviceInfo> checkedUsbTriggerDevices, BackgroundWorker backgroundWorker )
        {
            CheckedUsbTriggerDevices = checkedUsbTriggerDevices;
            _backgroundWorker = backgroundWorker;
            OnPrimaryPC = true;
        }

        private List<HIDAPI.HIDDeviceInfo> CheckedUsbTriggerDevices
        {
            get;set;
        }

        private List<DisplayDDC.MonitorSource[]> supportedMonitorSources
        {
            get; set;
        }

        public bool Running
        {
            get;set;
        }

        private bool OnPrimaryPC
        {
            get; set;
        }

        public bool TriggerDisplayCycle
        {
            get; set;
        }

        public void Start()
        {
            this.Running = true;
            bool found = false;

            while (this.Running)
            {
                Thread.Sleep(TimeSpan.FromSeconds(1));
                found = false;
                if (CheckedUsbTriggerDevices.Count == 0)
                    continue;

                List<HIDAPI.HIDDeviceInfo> devices = HIDAPI.HIDEnumerate(0, 0);

                var usbTriggerDevices = devices.OrderBy(o => o.product_string);//.Where(w => w.product_string.ToLower().Contains("trackball") || w.product_string.ToLower().Contains("mouse") || w.product_string.ToLower().Contains("keyboard"));
                foreach (HIDAPI.HIDDeviceInfo usbTriggerDevice in usbTriggerDevices)
                {
                    
                    if (usbTriggerDevice.product_string == null)
                        continue;

                    if (CheckedUsbTriggerDevices.Any(a => a.product_string == usbTriggerDevice.product_string))
                        found = true;

                    if (found && !OnPrimaryPC)
                    {                        
                        OnPrimaryPC = true;
                        this.TriggerDisplayCycle = true;
                        _backgroundWorker.ReportProgress(1);
                        break;
                    }                    
                }
                if (!found && OnPrimaryPC)
                {
                    this.OnPrimaryPC = false;
                    this.TriggerDisplayCycle = true;
                    _backgroundWorker.ReportProgress(2);
                }
            }
        }

        

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~TriggerHappyService()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
