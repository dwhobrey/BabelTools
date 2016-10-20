using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Babel.Usb {

    public class FTDIHandler {

        public const int FtdiVID = 0x0403; // FTDI VID.
        public const int FtdiPIDExcludeLo = 0x7e74; // Don't include devices in this range.
        public const int FtdiPIDExcludeHi = 0x7e77;

        public static bool IsFtdiDevice(int vid, int pid) {
            return (vid == FtdiVID)
                && ((pid< FtdiPIDExcludeLo) || (pid > FtdiPIDExcludeHi));
        }

        public static bool IsFtdiDevice(UsbLinkDevice dev) {
            return IsFtdiDevice(dev.DevVid,dev.DevPid);
        }

        /// <summary>
        /// Configure for FT230XS.
        /// </summary>
        /// <param name="dev">The usb device to try to configure.</param>
        /// <returns>Returns true on success.</returns>
        public static bool ConfigureFTDI(UsbLinkDevice dev) {
            byte request;
            ushort value;
            ushort index;
            // Reset command.
            request = 0x00;
            value = 0x0000; // Purge RX & TX.
            index = 0x1; // Port A.
            if(!dev.ControlTransfer(UsbCh9.USB_TYPE_VENDOR, request, value, index))
                return false;
            // Set modem control reg.
            request = 0x01;
            value = 0x0000; //0x0000 = DTR & RTS off.
            index = 0x1; // Port A.
            if(!dev.ControlTransfer(UsbCh9.USB_TYPE_VENDOR, request, value, index))
                return false;
            // Set flow control.
            request = 0x02;
            value = 0x0000; // No Xon & Xoff.
            index = 0x01; // Port A + no flow control.
            if(!dev.ControlTransfer(UsbCh9.USB_TYPE_VENDOR, request, value, index))
                return false;
            // Set baud rate. 
            request = 0x03;
            value = 0x0034; // 57600 => div=0x34, subdiv = 0x03 => 0xC000.
            index = 0x0; // Index used for Baud High, set bit 0 to zero. Not used for Port number.
            if(!dev.ControlTransfer(UsbCh9.USB_TYPE_VENDOR, request, value, index))
                return false;
            // Set data. 
            request = 0x04;
            value = 0x0008; // 8 bit data, no parity, 1 stop bit, no break.
            index = 0x1; // Port A.
            if(!dev.ControlTransfer(UsbCh9.USB_TYPE_VENDOR, request, value, index))
                return false;
            // Set event char. 
            request = 0x06;
            value = 0x0000; // None.
            index = 0x1; // Port A.
            if(!dev.ControlTransfer(UsbCh9.USB_TYPE_VENDOR, request, value, index))
                return false;
            // Set error char. 
            request = 0x07;
            value = 0x0000; // None.
            index = 0x1; // Port A.
            if(!dev.ControlTransfer(UsbCh9.USB_TYPE_VENDOR, request, value, index))
                return false;
            // Set latency timer. 
            request = 0x09;
            value = 0x002; // Or 2 ms!
            index = 0x1; // Port A.
            if(!dev.ControlTransfer(UsbCh9.USB_TYPE_VENDOR, request, value, index))
                return false;
            // Set bit mode. 
            request = 0x0B;
            value = 0x0000; // 0x0000 = Off. Or should it be 0x4000?
            index = 0x1; // Port A.
            if(!dev.ControlTransfer(UsbCh9.USB_TYPE_VENDOR, request, value, index))
                return false;
            return true;
        }
    }
}
