using System;
using System.Xml;
using System.Globalization;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Text;
using System.Diagnostics;
using Babel.Core;
using Babel.XLink;
using Jint;

using libusbK;

namespace Babel.Usb {

    public class UsbLinkDevice : LinkDevice {

        public const String TAG = "UsbLinkDevice";

        static int UsbMaxBufferSize = 2000;
        static int AllocCounter = 0;

        public int DevPid;
        public int DevVid;

        private byte ReadPipeId;
        private byte WritePipeId;
        private int TransferBufferSize;
        private int AltInterfaceId;

        private bool IsFTDI;
        private Object UsbLock;
        private UsbK DriverAPI;

        protected byte[] TempWriteBuffer;
        protected byte[] TempReadBuffer;
        protected byte[] TempControlBuffer;
        GCHandle gcHndWrite;
        GCHandle gcHndRead;
        GCHandle gcHndControl;

        int AllocId;

        public override string ToString() {
            return base.ToString() + ",Type=USB,Sid="+SessionId+",AId="+AllocId+",Pid=" + DevPid.ToString("X4") + ",Vid=" + DevVid.ToString("X4");
        }

        public static string GetDevIdFromHandle(KLST_DEVINFO_HANDLE d) {
            try {
                return d.SerialNumber;
            } catch (Exception) {
            }
            return "Unknown";
        }

        // Check that LinkDevice and DEVINFO refer to same device by comparing Vid & Pid.
        // Returns truee if they do.
        public static bool CompareVidPid(LinkDevice d, KLST_DEVINFO_HANDLE h) {
            UsbLinkDevice u = d as UsbLinkDevice;
            return (u != null && u.DevPid == h.Common.Pid && u.DevVid == h.Common.Vid);
        }

        public override bool HasHeartBeat() {
            return true;
        }

        // Note: set pipeId's to 0xff to pick default bulk endpoints.
        private UsbLinkDevice(KLST_DEVINFO_HANDLE deviceInfo, 
            byte readPipeId = 0xff, byte writePipeId = 0xff) {
                AllocId = ++AllocCounter;
            UsbLock = new Object();
            DriverAPI = null;
            IsFTDI = false;
            TempWriteBuffer = new byte[UsbMaxBufferSize];
            TempReadBuffer = new byte[UsbMaxBufferSize];
            TempControlBuffer = new byte[UsbMaxBufferSize];
            // SUPER IMPORTANT:
            // IO buffers MUST be pinned to stop GC moving them,
            // Otherwise read/writes will be corrupted!
            gcHndWrite = GCHandle.Alloc(TempWriteBuffer, GCHandleType.Pinned);
            gcHndRead = GCHandle.Alloc(TempReadBuffer, GCHandleType.Pinned);
            gcHndControl = GCHandle.Alloc(TempControlBuffer, GCHandleType.Pinned);
            InitDevice(deviceInfo, readPipeId, writePipeId);
        }

        private void InitDevice(KLST_DEVINFO_HANDLE deviceInfo,
            byte readPipeId = 0xff, byte writePipeId = 0xff) {
            ReadPipeId = (byte)(readPipeId | 0x80);
            WritePipeId = (byte)(writePipeId & 0x7f);
            TransferBufferSize = -1;
            AltInterfaceId = 0;
            //MaxTransfersTotal = 1024;
            //MaxPendingIO = 4;
            //MaxPendingTransfers = 64;
            try {
                Id = deviceInfo.SerialNumber;
            } catch (Exception) {
                Id = "Unknown";
            }
            try {
                DevPid = deviceInfo.Common.Pid;
            } catch (Exception) {
                DevPid = 0xffff;
            }
            try {
                DevVid = deviceInfo.Common.Vid;
            } catch (Exception) {
                DevVid = 0xffff;
            }
        }

        private void CloseDevice() {
            lock (UsbLock) {
                if (DriverAPI != null) {
                    DriverAPI.Free(); // Problematic...
                    DriverAPI = null;
                }
            }
        }

        public override void Close() {
            base.Close();
            try {
                gcHndWrite.Free();
            } catch (Exception) {
            }
            try {
                gcHndRead.Free();
            } catch (Exception) {
            }
            try {
                gcHndControl.Free();
            } catch (Exception) {
            }
        }

        protected override void DriverClose() {
            base.DriverClose();
            CloseDevice();
        }

        public static String DeviceInfoToString(KLST_DEVINFO_HANDLE deviceInfo) {
            return "deviceInfo.(Pid=" + deviceInfo.Common.Pid.ToString("X4") + ",Vid=" + deviceInfo.Common.Vid.ToString("X4")+")";
        }

        public void Disconnect(KLST_DEVINFO_HANDLE deviceInfo) {
            if (Settings.DebugLevel > 4) {
                Log.d(TAG, "Disconnect: u=" + this.ToString());
                Log.d(TAG, "Disconnect: " + DeviceInfoToString(deviceInfo));
            }
            if (CompareVidPid(this, deviceInfo)) {
                if (Settings.DebugLevel > 4) {
                    Log.d(TAG, "Disconnect: suspend.");
                }
                Suspend();
                DriverClose();
            }
        }

        protected override void InterruptRead() {
            lock (UsbLock) {
                if (DriverAPI != null)
                    DriverAPI.AbortPipe(ReadPipeId);
            }
        }
        protected override void InterruptWrite() {
            lock (UsbLock) {
                if (DriverAPI != null)
                    DriverAPI.AbortPipe(WritePipeId);
            }
        }

        protected override byte[] DriverRead() {
            int bytesRead, buffOffset;
            byte[] p = null;
            try {
                if (DriverAPI != null) {
                    while (true) {
                        bytesRead = 0;
                        if (DriverAPI != null) {
                            if (DriverAPI.ReadPipe(ReadPipeId, TempReadBuffer, TempReadBuffer.Length, out bytesRead, IntPtr.Zero)) {
                                if (bytesRead > 0) {
                                    if (IsFTDI) {
                                        if (bytesRead <= 2) { // It's just a status packet.
                                            continue;
                                        }
                                        bytesRead -= 2;
                                        buffOffset = 2;
                                    } else {
                                        buffOffset = 0;
                                    }
                                    p = new byte[bytesRead];
                                    Buffer.BlockCopy(TempReadBuffer, buffOffset, p, 0, bytesRead);
                                    if(Settings.DebugLevel>7) Log.d(TAG, "DriverRead Bytes:" + bytesRead + "\n");
                                    break;
                                }
                            } else {
                                if (Settings.DebugLevel > 8) Log.d(TAG, "DriverRead " + Error.Message() + "\n");
                            }
                        }
                        if (IsInterrupted) break;
                    }
                }
            } catch (Exception) {
            }
            return p;
        }

        protected override int DriverWrite(byte[] p) {
            int bytesWritten = 0;
            try {
                if (DriverAPI != null) {
                    int numBytesToWrite = p.Length;
                    if (numBytesToWrite > 0) {
                        Array.Copy(p, TempWriteBuffer, numBytesToWrite);
                        if (DriverAPI.WritePipe(WritePipeId, TempWriteBuffer, p.Length, out bytesWritten, IntPtr.Zero)) {
                            if (bytesWritten > 0) {
                                if (Settings.DebugLevel > 7) {
                                    Log.d(TAG, "DriverWrite Bytes:" + numBytesToWrite + "\n");
                                    String s = "{";
                                    for (int k = 0; k < p.Length; k++) {
                                        s += String.Format("{0:x2} ", p[k]);
                                    }
                                    s += "}\n";
                                    Log.d(TAG, s);
                                }
                            }
                        } else {
                            if (Settings.DebugLevel > 8) Log.d(TAG, "DriverWrite " + Error.Message() + "\n");
                        }
                    }
                }
            } catch (Exception e) {
                if (Settings.DebugLevel > 0) Log.d(TAG, "DriverWrite exception:" + e.Message);
            }
            return bytesWritten;
        }

        public bool ControlTransfer(byte requestType, byte request, ushort value,
                ushort index, Array buffer, int inLength, int timeout, out int outLength) {
            outLength = 0;
            libusbK.WINUSB_SETUP_PACKET packet;
            packet.RequestType = requestType;
            packet.Request = request;
            packet.Value = value;
            packet.Index = index;
            packet.Length = (ushort)inLength;
            if (buffer != null) Array.Copy(buffer, TempControlBuffer, inLength);
            bool result = false;
            lock (UsbLock) {
                try {
                    result = DriverAPI.ControlTransfer(packet, TempControlBuffer, inLength, out outLength, IntPtr.Zero);
                } catch (Exception) {

                }
            }
            if (result && (buffer != null) && (outLength>0)) {
                Array.Copy(TempControlBuffer, buffer, outLength);
            }
            return result;
        }

        public bool ControlTransfer(byte requestType, byte request, ushort value,
                ushort index, Array buffer=null, int inLength=0, int timeout=0) {
            int outLength;
            return ControlTransfer(requestType, request, value, index, buffer, inLength, timeout, out outLength); 
        }

        public bool SetConfiguration(ushort configNum) {
            return ControlTransfer(UsbCh9.USB_TYPE_STANDARD, UsbCh9.USB_REQ_SET_CONFIGURATION, configNum, 0);
        }

        public bool GetConfiguration(out int configNum) {
            byte[] ioBuffer = new byte[2];
            int outLen = 0;
            bool response;
            configNum = 0;
            // Note: this clears strings on device too.
            response = ControlTransfer((byte)(UsbCh9.USB_DIR_IN | UsbCh9.USB_TYPE_STANDARD), UsbCh9.USB_REQ_GET_CONFIGURATION, 0, 0, ioBuffer, 2, 0, out outLen);
            if(response && outLen==1)
                configNum = ioBuffer[0];
            return response;
        }

        // As well as opening the device:
        // Try putting it into accessory mode as necessary.
        // Returns null if unable to open or it was switched into accessory mode.
        // The steps for determining whether to connect to a usb device are:
        // 1) Consult vid/pid include & exclude lists.
        // 2) Open device.
        public static void TryOpeningDevice(KLST_DEVINFO_HANDLE deviceInfo) {
            bool isNewDev, checkForAccessory;
            string devId = GetDevIdFromHandle(deviceInfo);
            LinkDevice d = LinkManager.Manager.FindDevice(devId);
            UsbLinkDevice u = d as UsbLinkDevice;
            if (u != null) {
                if (u.State == ComponentState.Working || u.State == ComponentState.Unresponsive) {
                    if (Settings.DebugLevel > 4) Log.d(TAG, "TryOpeningDevice: already open:" + DeviceInfoToString(deviceInfo));
                    return;
                }
                // Check if d is a suspended Acc with same SN but different V/P.
                checkForAccessory = true; // !CompareVidPid(d, deviceInfo);
                u.CloseDevice(); //FIX: should already be closed?
                u.InitDevice(deviceInfo);
                isNewDev = false;
            }else {
                checkForAccessory = true; 
                u = new UsbLinkDevice(deviceInfo);     
                isNewDev = true;
            } 
            string result = null;
            lock (u.UsbLock) {
                result = u.ConfigureDevice(deviceInfo, checkForAccessory);
            }
            if (result == null) {
                u.SetSessionId();    
                if (isNewDev) {
                    LinkManager.Manager.AddDevice(u.Id, u);
                    u.InitThreads();
                    u.NotifyStateChange(ComponentState.Working);
                } else {
                    u.Resume();
                }
                    
            } else {
                u.DriverClose();
                if (Settings.DebugLevel > 4) Log.d(TAG, "ConfigureDevice: " + result);
                if (!isNewDev) {           
                    u.NotifyStateChange(ComponentState.Problem);
                } else { // Gets here if device was switched to acc mode.
                    u.Id = null; // Prevents LinkManager.DeleteDevice being called on Close.
                }
            }
        }

        // When device is (re)connected, enumerate endpoints and set policy.
        // Returns null on success or error message.
        private string ConfigureDevice(KLST_DEVINFO_HANDLE deviceInfo, bool checkForAccessory = false) {
            IsWriting = false;
            IsReading = false;
            if (State != ComponentState.Unresponsive || checkForAccessory || DriverAPI == null) {
                // libusbK class contructors can throw exceptions; For instance, if the device is
                // using the WinUsb driver and already in-use by another application.
                // This could happen if this App was previsouly aborted.
                try {
                    if (DriverAPI != null) {
                        DriverClose();
                    }
                    DriverAPI = new UsbK(deviceInfo);
                } catch (Exception e) {
                    DriverAPI = null;
                    return "Unable to initialize device:" + e.Message;
                }
            }
            try {
                DriverAPI.ResetDevice();
            } catch (Exception e) {
                return "Unable to reset device:" + e.Message;
            }

            // Find Pipe And Interface.
            byte interfaceIndex = 0; bool hasRead = false, hasWrite = false;
            USB_INTERFACE_DESCRIPTOR InterfaceDescriptor;
            WINUSB_PIPE_INFORMATION PipeInfo;
            while (DriverAPI.SelectInterface(interfaceIndex, true)) {
                byte altSettingNumber = 0;
                while (DriverAPI.QueryInterfaceSettings(altSettingNumber, out InterfaceDescriptor)) {
                    if (AltInterfaceId == -1 || AltInterfaceId == altSettingNumber) {
                        byte pipeIndex = 0;
                        while (DriverAPI.QueryPipe(altSettingNumber, pipeIndex++, out PipeInfo)) {
                            if (PipeInfo.MaximumPacketSize > 0) {
                                if (!hasRead &&
                                    ((PipeInfo.PipeId == ReadPipeId)
                                    || ((((ReadPipeId & 0xF) == 0) || (ReadPipeId == 0xff))
                                        && ((ReadPipeId & 0x80) == (PipeInfo.PipeId & 0x80))))
                                    ) {
                                    ReadPipeId = PipeInfo.PipeId;
                                    hasRead = true;
                                }
                                if (!hasWrite &&
                                    ((PipeInfo.PipeId == WritePipeId)
                                    || ((((WritePipeId & 0xF) == 0) || (WritePipeId == 0x7f))
                                        && ((WritePipeId & 0x80) == (PipeInfo.PipeId & 0x80))))
                                    ) {
                                    WritePipeId = PipeInfo.PipeId;
                                    hasWrite = true;
                                    if (TransferBufferSize == -1)
                                        TransferBufferSize = PipeInfo.MaximumPacketSize;
                                }
                                if (hasRead && hasWrite) goto FindInterfaceDone;
                            }
                            PipeInfo.PipeId = 0;
                        }
                    }
                    altSettingNumber++;
                }
                interfaceIndex++;
            }
        FindInterfaceDone:
            if (!hasRead && !hasWrite) {
                return "Unable to open i/o pipes:R="+hasRead+",W="+hasWrite+"."; 
            }

            ReadPipeId |= 0x80;
            WritePipeId &= 0x7f;
            if (TransferBufferSize == -1)
                TransferBufferSize = 64;
#if false // TODO: should test this.
            // Set interface alt setting.
            if (!DriverAPI.SetAltInterface(InterfaceDescriptor.bInterfaceNumber, false,
                                            InterfaceDescriptor.bAlternateSetting)) {
                return "Unable to set Alt Interface";
            }
#endif
            bool isAccessory = UsbLinkAccessory.IsAccessory(deviceInfo.Common.Vid,deviceInfo.Common.Pid);

            // Set configuration for accessory.               
            if (isAccessory) {
                int configNum=0;
                if(GetConfiguration(out configNum)) {
                    if(configNum!=1) {
                        if (!SetConfiguration(1)) {
                            return "Unable to set configuration";
                        }
                    }
                }
            }               

            // In most cases, the pipe timeout policy should be set before using synchronous I/O.
            // By default, sync transfers wait infinitely for a transfer to complete.
            // Set the pipe timeout policy to 0 for infinite timeout.
            int[] pipeTimeoutMS = new[] { 0 };
            int[] autoClearStall = new[] { 1 };
            DriverAPI.SetPipePolicy(ReadPipeId, (int)PipePolicyType.PIPE_TRANSFER_TIMEOUT,
                                Marshal.SizeOf(typeof(int)), pipeTimeoutMS);
            DriverAPI.SetPipePolicy(ReadPipeId, (int)PipePolicyType.AUTO_CLEAR_STALL,
                                Marshal.SizeOf(typeof(int)), autoClearStall);
            DriverAPI.SetPipePolicy(WritePipeId, (int)PipePolicyType.PIPE_TRANSFER_TIMEOUT,
                                Marshal.SizeOf(typeof(int)), pipeTimeoutMS);
            DriverAPI.SetPipePolicy(WritePipeId, (int)PipePolicyType.AUTO_CLEAR_STALL,
                                Marshal.SizeOf(typeof(int)), autoClearStall);
            /*
            int[] useRawIO = new[] { 1 };
            mUsb.SetPipePolicy(mReadPipeId, (int)PipePolicyType.RAW_IO,
                                Marshal.SizeOf(typeof(int)), useRawIO);
            */

            // Next, check if it's an accessory.
            if (checkForAccessory 
                && !isAccessory
                && UsbLinkAccessory.TryOpeningAccessory(this)) {
                return "Switching to accessory mode.";
            }

            // Finally, check if it's an FTDI device.
            IsFTDI = FTDIHandler.IsFtdiDevice(this);
            if (IsFTDI) {
                if (!FTDIHandler.ConfigureFTDI(this)) {
                    return "Unable to configure FTDI device.";
                }
            }
            return null;
        }
    }
}