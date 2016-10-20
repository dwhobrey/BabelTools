using System;

namespace Babel.BabelProtocol {

    public class ProtocolConstants {

        public const byte BYTE_SYNC = 0x55;
        public const byte BYTE_ESC = 0xAA; // Escape code for following byte.
        public const byte BYTE_ESC_SYNC = 0x11; // Following ESC, maps to a plain 0x55 byte.
        public const byte BYTE_ESC_ESC = 0x22;  // Following ESC, maps to a plain 0xAA byte.
        public const byte VNO_SIZE = 200;  // Max sequence value before cycling. Must be < 232.
        public const byte VNO_DELTA = 16;   // Amount by which next valid Vno may differ from last.
        public const byte VNO_NULL = 0xff; // Indicates a null Vno.
        public const byte PID_ARG_RESET = 0xfc; // Ping as arg tells receiver to flush/clear link.
        public const byte PID_ARG_SLAVE = 0xfd; // Ping as arg broadcasts adrs as a slave on link requesting master adrs.
        public const byte PID_ARG_MULTI = 0xfe; // Ping as arg broadcasts adrs as the master for this multi link.
        public const byte PID_ARG_MASTER = 0xff; // Ping as arg broadcasts adrs as the master for this uni link.
        public const byte PID_ARG_BAUD_NUM = 10;   // Reserve this many baud rate codes below BX_PID_ARG_RESET.
        public const byte PID_ARG_BAUD_MIN = (PID_ARG_RESET - PID_ARG_BAUD_NUM); // First baud rate code (9600).

        public const byte META_FLAGS_NONE = 0x00;
        public const byte META_FLAGS_RESEND = 0x10; // Set indicates resend message with original Vno.
        public const byte META_FLAGS_PID = 0x0f; // For extracting pid from flags.
        public const byte META_FLAGS_MASK = 0xf0; // For masking out pid.

        public const byte MESSAGE_FLAGS_IS_REPLY = 0x10; // Set indicates message is inbound, a reply.
        public const byte MESSAGE_FLAGS_ACK = 0x20; // Set indicates sender expects acknowledgment.
        public const byte MESSAGE_FLAGS_ORDER = 0x40; // Message has order details in data after cmd.
        public const byte MESSAGE_FLAGS_MASK = 0xf0; // For masking flags.
        public const byte MESSAGE_PORTS_MASK = 0x0f; // For masking ports.

        // Pid / Packet kind:
        public const byte PID_PING = 0x0; // Command to ping receiver, with next Vno.
        public const byte PID_REPLY = 0x1; // Reply to a ping, with Wno.
        public const byte PID_RESEND = 0x2; // Resend message Vno.
        public const byte PID_CANCEL = 0x3; // Cancel message Vno.
        public const byte PID_GENERAL = 0x4; // General message.
        public const byte PID_GENERAL_V = 0x5; // General message + verify Vno.
        public const byte PID_HANDSHAKE_MAX = 0x3;
        public const byte PID_MASK = 0xf;

        public const ushort ADRS_BROADCAST = 0xffff; // Broadcast to all devices.
        public const ushort ADRS_MULTICAST = 0xff00; // Multicast to all devices in group.
        public const ushort ADRS_LOCAL = 0x0000; // Refers to the device itself.

        public const byte NETIF_MEDIATOR_PORT = 0x0; // Dual NetIf & Port numbers.  
        public const byte NETIF_BRIDGE_PORT = 0x1;
        public const byte NETIF_C_PORT = 0x2;
        public const byte NETIF_D_PORT = 0x3;
        public const byte NETIF_BRIDGE_LINK = 0x4;
        public const byte NETIF_USER_BASE = 0x5; // First user netIf.
        public const byte NETIF_UNSET = 0xf;  // Indicates netIf unset.
        public const byte NETIF_NUM_SIZE = 0xf; // Maximum number of netIfs.

        public const byte PORT_MEDIATOR = 0x0;
        public const byte PORT_BRIDGE = 0x1;
        public const byte PORT_C = 0x2;
        public const byte PORT_D = 0x3;
        public const byte PORT_MASK = 0x3;

        // Standard device commands {1 to 15}:
        public const byte MEDIATOR_DEVICE_RESET = 1; // Reset device.
        public const byte MEDIATOR_DEVICE_STATUS = 2; // Get device status.
        public const byte MEDIATOR_DEVICE_TICKER = 3; // Get device ticker count.
        public const byte MEDIATOR_DEVICE_SETSN = 4; // Set Adrs, SN, save to EEPROM.
        public const byte MEDIATOR_DEVICE_GETSN = 5; // Get Adrs, SN of device at end of link.
        public const byte MEDIATOR_DEVICE_ERASE = 6; // Erase eeprom and restart.
        public const byte MEDIATOR_DEVICE_SETKEY = 7; // Set SysKey, save to EEPROM.
        public const byte MEDIATOR_DEVICE_READVAR = 8; // Read variable value in binary.
        public const byte MEDIATOR_DEVICE_WRITEVAR = 9; // Write variable value in binary.
        public const byte MEDIATOR_DEVICE_ISOVAR = 10; // Isochronous read variable value in binary.
        public const byte MEDIATOR_DEVICE_ISOMONVAR = 11; // Monitor read variable value in binary.
        public const byte MEDIATOR_DEVICE_ISOMSG = 12; // Isochronous message.
        public const byte MEDIATOR_DEVICE_LOG = 13; // Log message.

        // Connection commands {16 to 31}:
        public const byte MEDIATOR_CONNECT_ATTACH = 16; // Report attach and SN of device.
        public const byte MEDIATOR_CONNECT_DETACH = 17; // Report detach of device.
        public const byte MEDIATOR_CONNECT_GATEWAY = 18; // Register as a gateway.

        public const byte MEDIATOR_CONTROL_CMD_BASE = 32; // First control command code available to user.

        // Read/Write Var Flags can be OR'd together:
        public const byte MEDIATOR_DEVICE_RWV_FLAGS_RAM = 0x01; // Access ram value.
        public const byte MEDIATOR_DEVICE_RWV_FLAGS_EEPROM = 0x02; // Access eeprom value.
        public const byte MEDIATOR_DEVICE_RWV_FLAGS_DEFAULT = 0x04; // Access default value.
        public const byte MEDIATOR_DEVICE_RWV_FLAGS_RANGE = 0x08; // Access meta data: dsk, size, min, max.
        public const byte MEDIATOR_DEVICE_RWV_FLAGS_NAME = 0x10; // Access name.
        public const byte MEDIATOR_DEVICE_RWV_FLAGS_BY_NAME = 0x20; // Use name to lookup parameter.
        public const byte MEDIATOR_DEVICE_RWV_FLAGS_PACK = 0x40; // Pack write return values.
        public const byte MEDIATOR_DEVICE_RWV_FLAGS_PACK_KIND = 0x80; // Pack kind only + value.

        // In the following the ChkSum overhead is treated as part of data array.
        public const byte PACKET_SYNC_OFFSET = 0;
        public const byte PACKET_PID_OFFSET = 1;
        public const byte CHECK_START_OFFSET = 2; // First byte to check in header.
        public const byte PACKET_LINKID_OFFSET = 2;
        public const byte PACKET_ARG_OFFSET = 4;
        //
        public const byte MAX_PACKET_SIZE = 64; // Based on max size of a usb packet.
        public const byte ADDRESS_PARAMS_SIZE = 6;
        public const byte ORDER_PARAMS_SIZE = 2;
        public const byte META_SIZE = 4;
        public const byte PACKET_HEADER_SIZE = 5;
        public const byte CHECKSUM_SIZE = 1;
        public const byte TICKER_SIZE = 4;
        public const byte HANDSHAKE_PACKET_SIZE = (byte)(PACKET_HEADER_SIZE + CHECKSUM_SIZE);
        public const byte HANDSHAKE_CHECK_SIZE = (byte)(HANDSHAKE_PACKET_SIZE - CHECK_START_OFFSET);
        public const byte GENERAL_TRAITS_SIZE = 2; // Command, Data Length.
        public const byte GENERAL_CONTENT_SIZE = (byte)(ADDRESS_PARAMS_SIZE + GENERAL_TRAITS_SIZE);
        public const byte GENERAL_HEADER_SIZE = (byte)(PACKET_HEADER_SIZE + GENERAL_CONTENT_SIZE);
        public const byte GENERAL_TAIL_SIZE = (byte)(MAX_PACKET_SIZE - GENERAL_HEADER_SIZE);
        public const byte GENERAL_OVERHEADS_SIZE = (byte)(GENERAL_HEADER_SIZE + CHECKSUM_SIZE);
        public const byte GENERAL_MAX_DATA_SIZE = (byte)(MAX_PACKET_SIZE - GENERAL_OVERHEADS_SIZE);
        public const byte SERIAL_NUM_ASCII_SIZE = 32; // Max size as ascii rather than unicode.
        public const byte MAX_PARAMETER_NAME_SIZE = 8; // Max size of names in parameter table.

        public const byte GENERAL_DATA_LENGTH_OFFSET = (byte)(PACKET_HEADER_SIZE + GENERAL_CONTENT_SIZE - 1);
        public const byte GENERAL_DATA_ARRAY_OFFSET = (byte)(PACKET_HEADER_SIZE + GENERAL_CONTENT_SIZE);

        public const byte PARAMETER_TABLE_INDEX_PARAMS = 0;
        public const byte PARAMETER_TABLE_INDEX_PAGES = 1;
        public const byte PARAMETER_TABLE_INDEX_TICKER = 2;
        public const byte PARAMETER_TABLE_INDEX_HARDTYP = 3;
        public const byte PARAMETER_TABLE_INDEX_HARDVER = 4;
        public const byte PARAMETER_TABLE_INDEX_SOFTTYP = 5;
        public const byte PARAMETER_TABLE_INDEX_SOFTVER = 6;
        public const byte PARAMETER_TABLE_INDEX_SYSKEY = 7;
        public const byte PARAMETER_TABLE_INDEX_SERIALNO = 8;
        public const byte PARAMETER_TABLE_INDEX_PRONAME = 9;

        // Message Identifiers used internally to distinguish origin of replies.
        public const byte IDENT_MEDIATOR = 0x00; // For mediator port.
        public const byte IDENT_BRIDGE = 0x01;
        public const byte IDENT_C = 0x02;
        public const byte IDENT_D = 0x03;
        public const byte IDENT_READ = 0x04;
        public const byte IDENT_WRITE = 0x05;
        public const byte IDENT_VERIFY = 0x06;
        public const byte IDENT_MONITOR = 0x07; // For connection monitoring.
        public const byte IDENT_CONTROL = 0x08; // For control port.
        public const byte IDENT_COMMS = 0x09; // For comms port.
        public const byte IDENT_TEST = 0x0A; // For testing.
        public const byte IDENT_USER = 0x10; // First user ident.

        // Dispatch Action for message transaction.
        public const byte DISPATCH_POST_TO_NONE = 0;
        public const byte DISPATCH_SEND_TO_ROUTER = 1;
        public const byte DISPATCH_POST_TO_NETIF = 2;

        // Finish Action for message transaction.
        public const byte FINISH_NORMAL = 0;
        public const byte FINISH_FREE = 1; // Simply frees message buffer.
        public const byte FINISH_KEEP = 2; // Simply keeps message buffer.

        // Actions for link message verification.
        public const byte VERIFY_ACTION_NONE = 0; // No verification action required.
        public const byte VERIFY_ACTION_NEW = 1; // Add message to verification Q.
        public const byte VERIFY_ACTION_RESEND = 2; // Message being resent, already in Q. 

        // Actions for link baud rate protocol.
        public const byte BAUD_ACTION_SET = 0; // Set baud rate & clear trigger.
        public const byte BAUD_ACTION_SAVE = 1; // Set baud rate, clear trigger and save baudIndex to eeprom.
        public const byte BAUD_ACTION_SIGNAL = 2; // Trigger baud change by setting UART_*_SetBaudRate = netIfIndex.
    }
}