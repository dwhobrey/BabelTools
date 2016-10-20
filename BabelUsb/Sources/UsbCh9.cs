namespace Babel.Usb {
    public class UsbCh9 {
        // CONTROL REQUEST SUPPORT
        // This comes from the linux header file ch9.h.
        /*
         * USB directions.
         *
         * This bit flag is used in endpoint descriptors' bEndpointAddress field.
         * It's also one of three fields in control requests bRequestType.
         */
        public const byte USB_DIR_OUT = 0;   /* to device */
        public const byte USB_DIR_IN = 0x80; /* to host */
        /*
         * USB types, the second of three bRequestType fields.
         */
        public const byte USB_TYPE_MASK = (0x03 << 5);
        public const byte USB_TYPE_STANDARD = (0x00 << 5);
        public const byte USB_TYPE_CLASS = (0x01 << 5);
        public const byte USB_TYPE_VENDOR = (0x02 << 5);
        public const byte USB_TYPE_RESERVED = (0x03 << 5);
        /*
         * USB recipients, the third of three bRequestType fields.
         */
        public const byte USB_RECIP_MASK = 0x1f;
        public const byte USB_RECIP_DEVICE = 0x00;
        public const byte USB_RECIP_INTERFACE = 0x01;
        public const byte USB_RECIP_ENDPOINT = 0x02;
        public const byte USB_RECIP_OTHER = 0x03;
        /* From Wireless USB 1.0 */
        public const byte USB_RECIP_PORT = 0x04;
        public const byte USB_RECIP_RPIPE = 0x05;
        /*
         * Standard requests, for the bRequest field of a SETUP packet.
         *
         * These are qualified by the bRequestType field, so that for example
         * TYPE_CLASS or TYPE_VENDOR specific feature flags could be retrieved
         * by a GET_STATUS request.
         */
        public const byte USB_REQ_GET_STATUS = 0x00;
        public const byte USB_REQ_CLEAR_FEATURE = 0x01;
        public const byte USB_REQ_SET_FEATURE = 0x03;
        public const byte USB_REQ_SET_ADDRESS = 0x05;
        public const byte USB_REQ_GET_DESCRIPTOR = 0x06;
        public const byte USB_REQ_SET_DESCRIPTOR = 0x07;
        public const byte USB_REQ_GET_CONFIGURATION = 0x08;
        public const byte USB_REQ_SET_CONFIGURATION = 0x09;
        public const byte USB_REQ_GET_INTERFACE = 0x0A;
        public const byte USB_REQ_SET_INTERFACE = 0x0B;
        public const byte USB_REQ_SYNCH_FRAME = 0x0C;
        public const byte USB_REQ_SET_SEL = 0x30;
        public const byte USB_REQ_SET_ENCRYPTION = 0x0D;   /* Wireless USB */
        public const byte USB_REQ_GET_ENCRYPTION = 0x0E;
        public const byte USB_REQ_RPIPE_ABORT = 0x0E;
        public const byte USB_REQ_SET_HANDSHAKE = 0x0F;
        public const byte USB_REQ_RPIPE_RESET = 0x0F;
        public const byte USB_REQ_GET_HANDSHAKE = 0x10;
        public const byte USB_REQ_SET_CONNECTION = 0x11;
        public const byte USB_REQ_SET_SECURITY_DATA = 0x12;
        public const byte USB_REQ_GET_SECURITY_DATA = 0x13;
        public const byte USB_REQ_SET_WUSB_DATA = 0x14;
        public const byte USB_REQ_LOOPBACK_DATA_WRITE = 0x15;
        public const byte USB_REQ_LOOPBACK_DATA_READ = 0x16;
        public const byte USB_REQ_SET_INTERFACE_DS = 0x17;
        /* The Link Power Management (LPM) ECN defines USB_REQ_TEST_AND_SET command,
         * used by hubs to put ports into a new L1 suspend state, except that it
         * forgot to define its number ...
         */
        /*
         * USB feature flags are written using USB_REQ_{CLEAR,SET}_FEATURE, and
         * are read as a bit array returned by USB_REQ_GET_STATUS.  (So there
         * are at most sixteen features of each type.)  Hubs may also support a
         * new USB_REQ_TEST_AND_SET_FEATURE to put ports into L1 suspend.
         */
        public const byte USB_DEVICE_SELF_POWERED = 0;  /* (read only) */
        public const byte USB_DEVICE_REMOTE_WAKEUP = 1; /* dev may initiate wakeup */
        public const byte USB_DEVICE_TEST_MODE = 2;     /* (wired high speed only) */
        public const byte USB_DEVICE_BATTERY = 2;       /* (wireless) */
        public const byte USB_DEVICE_B_HNP_ENABLE = 3;  /* (otg) dev may initiate HNP */
        public const byte USB_DEVICE_WUSB_DEVICE = 3;   /* (wireless)*/
        public const byte USB_DEVICE_A_HNP_SUPPORT = 4; /* (otg) RH port supports HNP */
        public const byte USB_DEVICE_A_ALT_HNP_SUPPORT = 5; /* (otg) other RH port does */
        public const byte USB_DEVICE_DEBUG_MODE = 6;    /* (special devices only) */
        /*
         * Test Mode Selectors
         * See USB 2.0 spec Table 9-7
         */
        public const byte TEST_J = 1;
        public const byte TEST_K = 2;
        public const byte TEST_SE0_NAK = 3;
        public const byte TEST_PACKET = 4;
        public const byte TEST_FORCE_EN = 5;
        /*
         * New Feature Selectors as added by USB 3.0
         * See USB 3.0 spec Table 9-6
         */
        public const byte USB_DEVICE_U1_ENABLE = 48;      /* dev may initiate U1 transition */
        public const byte USB_DEVICE_U2_ENABLE = 49;      /* dev may initiate U2 transition */
        public const byte USB_DEVICE_LTM_ENABLE = 50;     /* dev may send LTM */
        public const ushort USB_INTRF_FUNC_SUSPEND = 0;     /* function suspend */
        public const ushort USB_INTR_FUNC_SUSPEND_OPT_MASK = 0xFF00;
        /*
         * Suspend Options, Table 9-7 USB 3.0 spec;
         */
        public const ushort USB_INTRF_FUNC_SUSPEND_LP = (ushort)(1 << (8 + 0));
        public const ushort USB_INTRF_FUNC_SUSPEND_RW = (ushort)(1 << (8 + 1));
        public const byte USB_ENDPOINT_HALT = 0;       /* IN/OUT will STALL */
        /* Bit array elements as returned by the USB_REQ_GET_STATUS request. */
        public const byte USB_DEV_STAT_U1_ENABLED = 2; /* transition into U1 state */
        public const byte USB_DEV_STAT_U2_ENABLED = 3; /* transition into U2 state */
        public const byte USB_DEV_STAT_LTM_ENABLED = 4;/* Latency tolerance messages */
    }
}
