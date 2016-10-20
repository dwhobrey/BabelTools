using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Babel.Core;
using Babel.XLink;

namespace Babel.Usb {

    /// <summary>
    /// Talking to Android devices via usb.
    /// An android device can be in one of two states:
    /// a) In accessory mode, in which case its VID/PID will be:
    ///    18D1/2D00 or 18D1/2D01.
    /// b) Otherwise its in normal mode, in which case VID/PID depends on vendor.
    ///    For the Nexus 7, vid/pid=18D1/4E40..4E44.
    ///    For other Nexus devices: pid=4e11..4ee6 or 2c10..2c11.
    /// BF attempts to put Nexus devices into accessory mode.
    /// </summary>
    public class UsbLinkAccessory {

        // Google Accessory IDs.
        public const int ACCVID = 0x18D1;
        public const int ACCPID = 0x2D00;
        public const int ALTACCPID = 0x2D01;

        // Google codes for AOP.
        const byte UsbRqstInVendor = (byte)(UsbCh9.USB_DIR_IN | UsbCh9.USB_TYPE_VENDOR);
        const byte UsbRqstOutVendor = (byte)(UsbCh9.USB_DIR_OUT | UsbCh9.USB_TYPE_VENDOR);
        const byte ACCESSORY_GET_PROTOCOL = 51;
        const byte ACCESSORY_SEND_STRING  = 52;
        const byte ACCESSORY_START        = 53;

        const ushort ACCESSORY_STRING_MANUFACTURER = 0;
        const ushort ACCESSORY_STRING_MODEL        = 1;
        const ushort ACCESSORY_STRING_DESCRIPTION  = 2;
        const ushort ACCESSORY_STRING_VERSION      = 3;
        const ushort ACCESSORY_STRING_URI          = 4;
        const ushort ACCESSORY_STRING_SERIAL       = 5;

        public static int TmpDevVersion = 0;

        public static string ACCSerialNumber = "";

        public static bool IsAccessory(int vid, int pid) {
            return vid == ACCVID && (pid == ACCPID || pid == ALTACCPID);
        }

        // Returns true if device was switched into Android accessory mode.
        // Note: only use u for ControlTransfer since vid/pid may be stale.
        public static bool TryOpeningAccessory(UsbLinkDevice u) {
            byte[] ioBuffer = new byte[2];
            int outLen = 0;
            bool response;
            string serialNumber = ACCSerialNumber;
            if (String.IsNullOrWhiteSpace(serialNumber)||(serialNumber.Length<2)) {
                serialNumber = Settings.SerialNumberFormatter(Lima.GetComputerCode(),'B','X');     
            }

            // response = u.mUsb.ResetDevice();
            // if(!response) { ShowLastError(); return null; }

            // Note: this clears strings on device too.
            response = u.ControlTransfer(UsbRqstInVendor, ACCESSORY_GET_PROTOCOL, 0, 0, ioBuffer, 2, 0, out outLen);
            if (!response) return false;
            TmpDevVersion = ioBuffer[1] << 8 | ioBuffer[0]; //This should be >0 if Accessory mode supported.
            if (TmpDevVersion == 0) return false;

            Thread.Sleep(1000); // Allow space  after In cmd otherwise sometimes hangs on the next transfer.

            ioBuffer = Encoding.UTF8.GetBytes(Settings.AppManufacturer); // Strings must be in UTF8 encoding.
            response = u.ControlTransfer(UsbRqstOutVendor, ACCESSORY_SEND_STRING, 0, ACCESSORY_STRING_MANUFACTURER, ioBuffer, ioBuffer.Length);
            if (!response) return false;
            ioBuffer = Encoding.UTF8.GetBytes(Settings.AppModelName);
            response = u.ControlTransfer(UsbRqstOutVendor, ACCESSORY_SEND_STRING, 0, ACCESSORY_STRING_MODEL, ioBuffer, ioBuffer.Length);
            if (!response) return false;
            ioBuffer = Encoding.UTF8.GetBytes(Settings.AppDescription);
            response = u.ControlTransfer(UsbRqstOutVendor, ACCESSORY_SEND_STRING, 0, ACCESSORY_STRING_DESCRIPTION, ioBuffer, ioBuffer.Length);
            if (!response) return false;
            ioBuffer = Encoding.UTF8.GetBytes(Settings.AppVersion);
            response = u.ControlTransfer(UsbRqstOutVendor, ACCESSORY_SEND_STRING, 0, ACCESSORY_STRING_VERSION, ioBuffer, ioBuffer.Length);
            if (!response) return false;
            ioBuffer = Encoding.UTF8.GetBytes(Settings.AppURI);
            response = u.ControlTransfer(UsbRqstOutVendor, ACCESSORY_SEND_STRING, 0, ACCESSORY_STRING_URI, ioBuffer, ioBuffer.Length);
            if (!response) return false;
            ioBuffer = Encoding.UTF8.GetBytes(serialNumber);
            response = u.ControlTransfer(UsbRqstOutVendor, ACCESSORY_SEND_STRING, 0, ACCESSORY_STRING_SERIAL, ioBuffer, ioBuffer.Length);
            if (!response) return false;

            // Control request for starting device in accessory mode.
            // The host sends this after setting all its strings to the device.
            response = u.ControlTransfer(UsbRqstOutVendor, ACCESSORY_START, 0, 0);
            Thread.Sleep(2000); // Allow time to switch.
            return response;
        }
    }
}
