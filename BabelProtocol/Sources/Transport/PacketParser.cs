using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Babel.Core;
using Babel.XLink;

namespace Babel.BabelProtocol {

    public class PacketParser {

        public const String TAG = "PacketParser";

        public const byte PARSE_SYNC = 0; // Start state must be zero.
        public const byte PARSE_PID = 1;
        public const byte PARSE_HEADER = 2;
        public const byte PARSE_DATA = 3;
        public const byte PARSE_ESC_NONE = 0;
        public const byte PARSE_ESC_START = 1;
        public const byte PARSE_ESC_SECOND = 2;

        public NetIfManager Manager;
        public LinkDriver IoNetIf;
        public PacketBuffer HandshakePacket;
        public PacketBuffer CurrentPacket;
        public byte[] InputBuffer;
        public int ChkSum;
        public int IoIndex;
        public byte Pid, PktLen;
        public byte ParseState, ParseEscState, DataIndex;

        public PacketParser(LinkDriver ltx) {
            IoNetIf = ltx;
            Manager = ltx.Manager;
            HandshakePacket = new PacketBuffer(ProtocolConstants.HANDSHAKE_PACKET_SIZE);
            InputBuffer = null;
            CurrentPacket = null;
            IoIndex = -1;
            ResetParser();
        }

        public void ResetParser() {
            ParseState = PARSE_SYNC;
            ParseEscState = (IoNetIf.MappingEnabled ? PARSE_ESC_START : PARSE_ESC_NONE);
        }

        // On entry: ParseState is current protocol parser state.
        public void ProtocolParser(byte[] buffer) {
            int bufferIndex = 0, numRead = buffer.Length;
            while (bufferIndex < numRead) {
                byte b = buffer[bufferIndex++];
                switch (ParseEscState) {
                    case PARSE_ESC_SECOND:
                        ParseEscState = PARSE_ESC_START;
                        switch (b) {
                            case ProtocolConstants.BYTE_ESC_SYNC:
                                if (ParseState == PARSE_SYNC)
                                    continue;
                                b = ProtocolConstants.BYTE_SYNC;
                                break;
                            case ProtocolConstants.BYTE_ESC_ESC:
                                b = ProtocolConstants.BYTE_ESC;
                                break;
                            default:
                                break;
                        }
                        break;
                    case PARSE_ESC_START:
                        if (b == ProtocolConstants.BYTE_ESC) {
                            ParseEscState = PARSE_ESC_SECOND;
                            continue;
                        } else if (b == ProtocolConstants.BYTE_SYNC)
                            ParseState = PARSE_SYNC;
                        break;
                    default:
                        break;
                }
                switch (ParseState) {
                    case PARSE_SYNC:
                        if (b == ProtocolConstants.BYTE_SYNC)
                            ParseState = PARSE_PID;
                        break;
                    case PARSE_PID:
                        Pid = (byte)(b & ProtocolConstants.PID_MASK);
                        switch (Pid) {
                            case ProtocolConstants.PID_PING:
                            case ProtocolConstants.PID_REPLY:
                            case ProtocolConstants.PID_RESEND:
                            case ProtocolConstants.PID_CANCEL:
                                InputBuffer = HandshakePacket.buffer;
                                PktLen = ProtocolConstants.HANDSHAKE_PACKET_SIZE;
                                break;
                            case ProtocolConstants.PID_GENERAL:
                            case ProtocolConstants.PID_GENERAL_V:
                                if (IoIndex == -1) { // Setup a buffer.
                                    IoIndex = Manager.IoBuffersFreeHeap.Allocate();
                                    if (IoIndex == -1) {
                                        ParseState = PARSE_SYNC;
                                        continue;
                                    }
                                    CurrentPacket = Manager.IoBuffers[IoIndex];
                                }
                                InputBuffer = CurrentPacket.buffer;
                                PktLen = ProtocolConstants.GENERAL_HEADER_SIZE;
                                break;
                            default:
                                ParseState = PARSE_SYNC;
                                continue;
                        }
                        InputBuffer[ProtocolConstants.PACKET_SYNC_OFFSET] = ProtocolConstants.BYTE_SYNC;
                        InputBuffer[ProtocolConstants.PACKET_PID_OFFSET] = b;
                        DataIndex = ProtocolConstants.CHECK_START_OFFSET; // Skip over sync & pid.
                        ChkSum = 0;
                        ParseState = PARSE_HEADER;
                        break;
                    case PARSE_HEADER:
                        ChkSum += b;
                        InputBuffer[DataIndex++] = b;
                        if (DataIndex < PktLen)
                            break;
                        if (Pid > ProtocolConstants.PID_HANDSHAKE_MAX) {
                            byte len = InputBuffer[ProtocolConstants.GENERAL_DATA_LENGTH_OFFSET];
                            PktLen = (byte)(len + ProtocolConstants.GENERAL_OVERHEADS_SIZE);
                            if (len <= (Manager.MaxPacketSize - ProtocolConstants.GENERAL_OVERHEADS_SIZE)) {
                                ParseState = PARSE_DATA;
                                break;
                            }
                            if (Settings.DebugLevel > 2)
                                Log.w(TAG, "Bad message length:" + len + ".");
                        } else {
                            if ((ChkSum % 256) == 0) {
                                IoNetIf.Monitor.PerformLinkHandshake(HandshakePacket);
                                if (Settings.DebugLevel > 6)
                                    Log.d(TAG, Diagnostics.DumpHandshake(HandshakePacket));
                            } else {
                                if (Settings.DebugLevel > 2)
                                    Log.w(TAG, "Bad header chksum.");
                            }
                        }
                        ParseState = PARSE_SYNC;
                        break;
                    case PARSE_DATA:
                        ChkSum += b;
                        InputBuffer[DataIndex++] = b;
                        if (DataIndex < PktLen)
                            break;
                        if ((ChkSum % 256) != 0) { // Xfer error, ditch packet.
                            if (Settings.DebugLevel > 2)
                                Log.w(TAG, "Bad message chksum.");
                            ParseState = PARSE_SYNC;
                            break;
                        }
                        // By here there is a valid packet.
                        // Fill in some missing details:
                        CurrentPacket.flagsPid = Pid;
                        CurrentPacket.pktLen = PktLen;
                        CurrentPacket.iNetIf = IoNetIf.NetIfIndex;
                        CurrentPacket.dNetIf = ProtocolConstants.NETIF_UNSET;
                        if (Settings.DebugLevel > 6)
                            Log.d(TAG, Diagnostics.DumpMessage(CurrentPacket));
                        IoNetIf.Monitor.DispatchLinkPacket(this);
                        ParseState = PARSE_SYNC;
                        break;
                    default:
                        ParseState = PARSE_SYNC;
                        break;
                }
            }
        }
    }
}
