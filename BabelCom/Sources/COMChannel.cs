using System;
using System.Collections;
using System.Xml;
using System.Threading;
using System.IO;
using System.IO.Ports;
using Babel.Core;
using Babel.XLink;

namespace Babel.Com {

    public class COMChannel {

        static int COMId = 1; // Counter for next default COM port number.

        SerialPort Port = null;

        public COMChannel(XmlNode node) {
         //   Name = Project.GetNodeAttributeValue(node, "name", "Port" + ++COMId);
            Port = new SerialPort();
            Port.PortName = Project.GetNodeAttributeValue(node, "portname", "COM" + COMId);
            Port.BaudRate = Project.GetNodeAttributeValue(node, "baudrate", 115200);
            Port.Parity = (Parity)Enum.Parse(typeof(Parity), Project.GetNodeAttributeValue(node, "parity", "None"));
            Port.DataBits = Project.GetNodeAttributeValue(node, "databits", 8);
            Port.StopBits = (StopBits)Enum.Parse(typeof(StopBits), Project.GetNodeAttributeValue(node, "stopbits", "One"));
            Port.Handshake = (Handshake)Enum.Parse(typeof(Handshake), Project.GetNodeAttributeValue(node, "handshake", "None"));
            Port.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);
            Port.WriteTimeout = 5000;
            Port.Open();
          //  Init();
        }

        public COMChannel() {
            Port = new SerialPort();
            Port.PortName = "COM1";
            Port.BaudRate = 115200;
            Port.Parity = Parity.None;
            Port.DataBits = 8;
            Port.StopBits = StopBits.One;
            Port.Handshake = Handshake.None;
            Port.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);
            Port.WriteTimeout = 5000;
            Port.Open();
            //  Init();
        }

        public void Close() {
            Port.Close();
        }

        void DataReceivedHandler(object sender, SerialDataReceivedEventArgs e) {
           // if(!IsClosing) FlushBuffer();
        }

        public string ChannelStatus {
            get {
                return Port.PortName + ":" + Port.BaudRate;
            }
        }

        public void WriteData(byte[] buffer) {
            try {
                if (buffer != null && buffer.Length > 0)
                    Port.Write(buffer, 0, buffer.Length);
            } catch (Exception) {

            }
        }

        public byte[] ReadData() {
            int n = 0;
            try {
                n = Port.BytesToRead;
            } catch (Exception) {
                n = 0;
            }
            byte[] buffer = new byte[n];
            try {
                if (n > 0)
                    Port.Read(buffer, 0, n);
            } catch (Exception) {
            }
            return buffer;
        }
    }
}
