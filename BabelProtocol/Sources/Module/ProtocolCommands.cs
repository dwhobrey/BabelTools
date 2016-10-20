using System;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Babel.Core;
using Babel.XLink;
using Jint;

using System.IO;
using System.Windows;
using System.Xml;

namespace Babel.BabelProtocol {

    public class ProtocolCommands : MessageExchange.IMessageHandler {

        /*
         * Encapsulates an Isochronous read to MCU so that it can be serialised on project save or restore.
         * 
         */
        public class IsoRead {

            public static readonly long UpdateInterval = 10 * 10000 * 1000; // 10 secs in ticks.

            string ExchangeName;
            byte[] DataArray;
            bool IsMonitor;
            int IsoId;
            int Ident;
            int Addrs;
            int PostNetIfIndex;
            int MilliInterval;
            int RepeatCount;
            long LastUpdateTime;

            public IsoRead(byte[] dataArray,
                String exchangeName,
                bool isMonitor = true,
                int isoId = 1,
                int ident = ProtocolConstants.IDENT_USER,
                int postNetIf = ProtocolConstants.NETIF_USER_BASE,
                int addrs = ProtocolConstants.ADRS_LOCAL,
                int milliInterval = 100,
                int repeatCount = 0) {
                DataArray = dataArray;
                ExchangeName = exchangeName;
                IsMonitor = isMonitor;
                IsoId = isoId;
                Ident = ident;
                Addrs = addrs;
                PostNetIfIndex = postNetIf;
                MilliInterval = milliInterval;
                RepeatCount = repeatCount;
                LastUpdateTime = 0;
            }

            public IsoRead(XmlNode isoNode) {
                if (isoNode != null) {
                    DataArray = HexToByteArray(Project.GetNodeAttributeValue(isoNode, "array", ""));
                    IsMonitor = Convert.ToBoolean(Project.GetNodeAttributeValue(isoNode, "ismonitor", "true"));
                    ExchangeName = Project.GetNodeAttributeValue(isoNode, "exchangename", "");
                    IsoId = Convert.ToInt16(Project.GetNodeAttributeValue(isoNode, "isoid", "1"));
                    Ident = Convert.ToInt16(Project.GetNodeAttributeValue(isoNode, "ident", ProtocolConstants.IDENT_USER.ToString()));
                    Addrs = Convert.ToInt16(Project.GetNodeAttributeValue(isoNode, "addrs", ProtocolConstants.ADRS_LOCAL.ToString()));
                    PostNetIfIndex = Convert.ToInt16(Project.GetNodeAttributeValue(isoNode, "netif", ProtocolConstants.NETIF_USER_BASE.ToString()));
                    MilliInterval = Convert.ToInt16(Project.GetNodeAttributeValue(isoNode, "milliinterval", "100"));
                    RepeatCount = Convert.ToInt16(Project.GetNodeAttributeValue(isoNode, "repeatcount", "0"));

                }
                LastUpdateTime = 0;
            }

            public void Serialize(XmlNode isoNodes) {
                XmlNode isoNode = Project.GetChildNode(isoNodes, "isoread", Key());
                if (isoNode != null) {
                    Project.SetNodeAttributeValue(isoNode, "array", ByteArrayToHex(DataArray));
                    Project.SetNodeAttributeValue(isoNode, "ismonitor", IsMonitor.ToString());
                    Project.SetNodeAttributeValue(isoNode, "exchangename", ExchangeName);
                    Project.SetNodeAttributeValue(isoNode, "isoid", IsoId.ToString());
                    Project.SetNodeAttributeValue(isoNode, "ident", Ident.ToString());
                    Project.SetNodeAttributeValue(isoNode, "addrs", Addrs.ToString());
                    Project.SetNodeAttributeValue(isoNode, "netif", PostNetIfIndex.ToString());
                    Project.SetNodeAttributeValue(isoNode, "milliinterval", MilliInterval.ToString());
                    Project.SetNodeAttributeValue(isoNode, "repeatcount", RepeatCount.ToString());
                }
            }

            public static string ByteArrayToHex(byte[] data) {
                byte b;
                int i, j, k;
                int l = data.Length;
                char[] r = new char[l * 2];
                for (i = 0, j = 0; i < l; ++i) {
                    b = data[i];
                    k = b >> 4;
                    r[j++] = (char)(k > 9 ? k + 0x37 : k + 0x30);
                    k = b & 15;
                    r[j++] = (char)(k > 9 ? k + 0x37 : k + 0x30);
                }
                return new string(r);
            }

            public static byte[] HexToByteArray(string hex) {
                byte[] bytes = new byte[hex.Length / 2];
                int bl = bytes.Length;
                for (int i = 0; i < bl; ++i) {
                    bytes[i] = (byte)((hex[2 * i] > 'F' ? hex[2 * i] - 0x57 : hex[2 * i] > '9' ? hex[2 * i] - 0x37 : hex[2 * i] - 0x30) << 4);
                    bytes[i] |= (byte)(hex[2 * i + 1] > 'F' ? hex[2 * i + 1] - 0x57 : hex[2 * i + 1] > '9' ? hex[2 * i + 1] - 0x37 : hex[2 * i + 1] - 0x30);
                }
                return bytes;
            }

            public string Key() {
                return ExchangeName + Ident.ToString();
            }

            public bool IsCancel() {
                return MilliInterval == 0;
            }

            public void SetTime() {
                LastUpdateTime = DateTime.UtcNow.Ticks;
            }

            public long GetTime() {
                return LastUpdateTime;
            }

            /*
             * Sends IsoRead request to MCU.
             * When isCancel, send a message to cancel prior IsoRead.
             */
            public bool PostMessage(bool isCancel) {
                if (!String.IsNullOrWhiteSpace(ExchangeName)) {
                    ProtocolHub h = null;
                    Commander.Exchanges.TryGetValue(ExchangeName, out h);
                    if (h != null) {
                        if (isCancel) { // Hack: set milliInterval to zero to cancel.
                            DataArray[1] = 0;
                            DataArray[2] = 0;
                        }
                        BabelMessage message = BabelMessage.CreateCommandMessage(
                            h.Exchange, false, Router.RouterAction.PostToNetIf,
                            (byte)PostNetIfIndex, (IsMonitor ? ProtocolConstants.MEDIATOR_DEVICE_ISOMONVAR : ProtocolConstants.MEDIATOR_DEVICE_ISOVAR),
                            (ushort)Addrs, ProtocolConstants.ADRS_LOCAL, 0,
                            (byte)Ident, (byte)DataArray.Length, (byte)0, DataArray
                        );
                        if(isCancel) {
                            DataArray[1] = (byte)(MilliInterval & 0xff);
                            DataArray[2] = (byte)((MilliInterval >> 8) & 0xff);
                        }
                        if (message.Exchange.SubmitMessage(message, Commander, false, RepeatCount)) {
                            return true;
                        }
                    }
                }
                return false;
            }
        }

        public const String TAG = "ProtocolCommands";

        bool IsClosing;
        public int DefaultNetIfNumber;
        public static int CacheIdCounter;
        public Dictionary<string, BabelMessageDataCache> Caches;
        public Dictionary<string, ProtocolHub> Exchanges;
        // Map: ExchangeName+Ident to Iso Message.
        public Dictionary<string, IsoRead> IsoReads;
        Object IsoReadsLock;
        Thread IsoReadsHandlerTask;

        // Map: DevId to <ShellId, Exchange Name, NetIfIndex>.
        public Dictionary<string, Tuple<string, string, byte>> ExchangeMapping;

        public static ProtocolCommands Commander = new ProtocolCommands();

        private ProtocolCommands() {
            DefaultNetIfNumber = ProtocolConstants.NETIF_USER_BASE;
            Caches = new Dictionary<string, BabelMessageDataCache>();
            Exchanges = new Dictionary<string, ProtocolHub>();
            ExchangeMapping = new Dictionary<string, Tuple<string, string, byte>>();
            IsoReads = new Dictionary<string, IsoRead>();
            IsoReadsLock = new Object();
            IsoReadsHandlerTask = null;
        }

        private static void Serializer(XmlNode node, bool isSerialize) {
            XmlNode c = Project.GetChildNode(node, "bxprotocol");
            if (c == null) return;
            if (isSerialize) {
                // Serialize netif number.
                XmlNode dpn = Project.GetChildNode(c, "defaultnetifnumber");
                if (dpn != null) {
                    Project.SetNodeAttributeValue(dpn, "value", Commander.DefaultNetIfNumber.ToString());
                }
                // Serialize exchange mappings:
                XmlNode maps = Project.GetChildNode(c, "exchangemaps");
                if (maps != null) {
                    maps.RemoveAll();
                    foreach (KeyValuePair<string, Tuple<string, string, byte>> map in Commander.ExchangeMapping) {
                        XmlNode mapNode = Project.GetChildNode(maps, "map", map.Key);
                        if (mapNode != null) {
                            Project.SetNodeAttributeValue(mapNode, "shellidortitle", map.Value.Item1);
                            Project.SetNodeAttributeValue(mapNode, "exchangename", map.Value.Item2);
                            Project.SetNodeAttributeValue(mapNode, "netifnumber", map.Value.Item3);
                        }
                    }
                }
                // Serialize isoreads:
                XmlNode isoreads = Project.GetChildNode(c, "isoreads");
                if (isoreads != null) {
                    isoreads.RemoveAll();
                    foreach (KeyValuePair<string, IsoRead> ir in Commander.IsoReads) {
                        ir.Value.Serialize(isoreads);
                    }
                }
            } else {
                // Fetch netIf number.
                XmlNode dpn = Project.GetChildNode(c, "defaultnetiftnumber");
                if (dpn != null) {
                    Commander.DefaultNetIfNumber = Convert.ToInt16(Project.GetNodeAttributeValue(dpn, "value", "2"));
                }
                XmlNodeList nodeList = Project.GetNodes(c, "exchangemaps/*");
                if (nodeList != null) {
                    // Fetch exchange mappings:
                    foreach (XmlNode p in nodeList) {
                        string deviceIdPattern = Project.GetNodeAttributeValue(p, "name", "");
                        if (!String.IsNullOrWhiteSpace(deviceIdPattern)) {
                            string shellIdOrTitle = Project.GetNodeAttributeValue(p, "shellidortitle", "");
                            string exchangeName = Project.GetNodeAttributeValue(p, "exchangename", "");
                            string netIfIndex = Project.GetNodeAttributeValue(p, "netifnumber", "");
                            if (!String.IsNullOrWhiteSpace(shellIdOrTitle)
                                && !String.IsNullOrWhiteSpace(exchangeName)
                                && !String.IsNullOrWhiteSpace(netIfIndex)) {
                                Commander.ExchangeMapping[deviceIdPattern]
                                    = new Tuple<string, string, byte>(shellIdOrTitle, exchangeName, (byte)Convert.ToByte(netIfIndex));
                            }
                        }
                    }
                    BApply();
                }
                nodeList = Project.GetNodes(c, "isoreads/*");
                if (nodeList != null) {
                    // Fetch isoreads:
                    lock (Commander.IsoReadsLock) {
                        foreach (XmlNode p in nodeList) {
                            string key = Project.GetNodeAttributeValue(p, "name", "");
                            if (!String.IsNullOrWhiteSpace(key)) {
                                Commander.IsoReads[key] = new IsoRead(p);
                            }
                        }
                    }
                }
            }
        }

        public void UpdateIsoReads(IsoRead ir, bool forceCancel = false) {
            if (ir != null) {
                string key = ir.Key();
                lock (IsoReadsLock) {
                    if (forceCancel || ir.IsCancel()) {
                        ir.PostMessage(true);
                        Commander.IsoReads.Remove(key);
                    } else {
                        Commander.IsoReads[key] = ir;
                    }
                }
            }
        }

        public static BabelMessageDataCache GetCache(string name) {
            BabelMessageDataCache c = null;
            if (!String.IsNullOrWhiteSpace(name)) {
                Commander.Caches.TryGetValue(name, out c);
            }
            return c;
        }

        public static void RemoveCache(string name) {
            if (!String.IsNullOrWhiteSpace(name)) {
                BabelMessageDataCache c = null;
                Commander.Caches.TryGetValue(name, out c);
                if (c != null) {
                    IsoRead ir = null;
                    if (Commander.IsoReads.TryGetValue(c.GetExchangeName() + c.GetMessageId(), out ir)) {
                        Commander.UpdateIsoReads(ir,true);
                    }
                    Commander.Caches.Remove(name); // Do this first to stop recursion.
                    c.Close();                
                }
            }
        }

        public static void AddCache(BabelMessageDataCache c) {
            BabelMessageDataCache old;
            if (!String.IsNullOrWhiteSpace(c.GetCacheId())) {
                if (Commander.Caches.TryGetValue(c.GetCacheId(), out old)) {
                    old.Close();
                }
                if (c != null)
                    Commander.Caches.Add(c.GetCacheId(), c);
            }
        }

        private static void ClearProject() {
            CacheIdCounter = 0;
            List<BabelMessageDataCache> caches = new List<BabelMessageDataCache>(Commander.Caches.Values);
            foreach(BabelMessageDataCache d in caches) {
                if (d != null) {
                    d.Close(); // This will remove element from Caches.
                }
            }
            Commander.Caches.Clear();
            Commander.ExchangeMapping.Clear();
            foreach (ProtocolHub h in Commander.Exchanges.Values) {
                if (h != null) {
                    h.Close();
                }
            }
            Commander.Exchanges.Clear();
            lock (Commander.IsoReadsLock) {
                Commander.IsoReads.Clear();
            }
        }

        public static void ConfigProject() {
            ClearProject();
        }
        public static void StartProject() {
            LinkManager.Manager.AddListener(Commander, ComponentEventListener);
            Serializer(Project.GetNode(Project.ProjectRootNodeName), false);
            Commander.IsClosing = false;
            Commander.IsoReadsHandlerTask = new Thread(new ThreadStart(Commander.IsoReadsHandler));
            Commander.IsoReadsHandlerTask.Name = "IsoReadsHandlerThread";
            Commander.IsoReadsHandlerTask.Priority = ThreadPriority.Normal;
            Commander.IsoReadsHandlerTask.Start();
        }
        public static void SaveProject() {
            Serializer(Project.GetNode(Project.ProjectRootNodeName), true);
        }
        public static void CloseProject() {
            Commander.IsClosing = true;
            if (Commander.IsoReadsHandlerTask != null) {
                Primitives.Interrupt(Commander.IsoReadsHandlerTask);
                Commander.IsoReadsHandlerTask = null;
            }
            LinkManager.Manager.RemoveListener(Commander, ComponentEventListener);
            Serializer(Project.GetNode(Project.ProjectRootNodeName), true);
            ClearProject();
        }

        public void OnBabelMessage(BabelMessage msg) {
            ProtocolHub h = null;
            Commander.Exchanges.TryGetValue(msg.Exchange.Id, out h);
            if (h != null) {
                h.AddToQueue(msg.SenderId, msg);
                IsoRead ir = null;
                if (Commander.IsoReads.TryGetValue(msg.Exchange.Id + msg.SenderId, out ir)) {
                    ir.SetTime();
                }
            }
        }

        // Iterate through the exchange mapping patterns for a match on the device Id.
        // Sets tuple to the first matching exchange map or null if no match.
        // Returns true on success.
        public static bool FindExchangMap(string deviceId, out Tuple<string, string, byte> tuple) {
            if (!String.IsNullOrWhiteSpace(deviceId)) {
                foreach (string pattern in Commander.ExchangeMapping.Keys) {
                    if (Regex.IsMatch(deviceId, pattern, RegexOptions.IgnoreCase)) {
                        if (Commander.ExchangeMapping.TryGetValue(pattern, out tuple))
                            return true;
                    }
                }
            }
            tuple = null;
            return false;
        }

        /// <summary>
        /// Apply BME mappings to component as necessary.
        /// </summary>
        /// <param name="component"></param>
        public static void BApplyMappingsToDevice(LinkDevice d) {
            Tuple<string, string, byte> tuple = null;
            if (d != null && FindExchangMap(d.Id, out tuple)) {
                string exchangeName = tuple.Item2;
                ProtocolHub h = null;
                // Check if need to invoke a new exchange.
                if (exchangeName != null && !Commander.Exchanges.TryGetValue(exchangeName, out h)) {
                    MessageExchange me = new MessageExchange(tuple.Item1, exchangeName, null);
                    h = new ProtocolHub(me);
                    Commander.Exchanges.Add(exchangeName, h);
                    LinkManager.Manager.ComponentEventListener(ComponentEvent.ComponentAdd, me, null);
                }
                if (h != null && h.Exchange.Manager.GetLinkDriver(d.Id) == null) {
                    h.Exchange.AddListenerNetIf(d, tuple.Item3);
                }
            }
        }

        /// <summary>
        /// This listener is added to the LinkManager.
        /// It listens for events requiring invocation of a MessageExchange.
        /// </summary>
        /// <param name="d">The device the event occurred on.</param>
        /// <param name="e">The event.</param>
        /// <param name="v">The object associated with the event, such as a byte buffer or device state.</param>
        public static void ComponentEventListener(ComponentEvent ev, Component component, object val) {
            if (ComponentEvent.ComponentAdd == ev || ComponentEvent.ComponentResume == ev) { // See if new device needs to be mapped to an exchange.
                BApplyMappingsToDevice(component as LinkDevice);
            }
        }

        [ScriptFunction("bapply", "Apply BME mappings to device(s) as necessary.",
            typeof(Jint.Delegates.Func<String, String>), "Id device pattern.")]
        public static string BApply(string deviceIdPattern = "^.*") {
            if (!String.IsNullOrWhiteSpace(deviceIdPattern)) {
                List<LinkDevice> devs = LinkManager.Manager.GetDevices(deviceIdPattern);
                foreach (LinkDevice d in devs) {
                    BApplyMappingsToDevice(d);
                }
            }
            return "";
        }

        [ScriptFunction("bme", "Returns a list of the Babel message exchanges.",
            typeof(Jint.Delegates.Func<String>))]
        public static string GetBabelMessageExchanges() {
            string report = "bme:\n";
            foreach (ProtocolHub h in Commander.Exchanges.Values) {
                report += h.Exchange.ToString() + "\n";
            }
            return report;
        }

        [ScriptFunction("bopen", "Open a Babel message exchange.\nReturns empty string or error message.",
            typeof(Jint.Delegates.Func<String, String, String, String>),
            "Id of shell.", "Name for exchange.", "Master serial number.")]
        public static string BOpen(string shellId, string exchangeName, string masterSerialNumber = "") {
            if (!String.IsNullOrWhiteSpace(shellId) && !String.IsNullOrWhiteSpace(exchangeName)) {
                Shell s = Shell.GetShell(shellId);
                if (s != null) {
                    if (!Commander.Exchanges.ContainsKey(exchangeName)) {
                        MessageExchange me = new MessageExchange(shellId, exchangeName, masterSerialNumber);
                        ProtocolHub h = new ProtocolHub(me);
                        Commander.Exchanges.Add(exchangeName, h);
                        LinkManager.Manager.ComponentEventListener(ComponentEvent.ComponentAdd, me, null);
                    }
                    return "";
                }
                return "Error: bad shell Id.";
            }
            return "Error: bad parameter.";
        }

        [ScriptFunction("bclose", "Close a Babel message exchange.\nReturns empty string or error message.",
            typeof(Jint.Delegates.Func<String, String>),
            "Name of exchange.")]
        public static string BClose(string exchangeName) {
            if (!String.IsNullOrWhiteSpace(exchangeName)) {
                ProtocolHub h;
                if (Commander.Exchanges.TryGetValue(exchangeName, out h)) {
                    LinkManager.Manager.ComponentEventListener(ComponentEvent.ComponentRemove, h.Exchange, null);
                    h.Close();
                    Commander.Exchanges.Remove(exchangeName);
                }
                return "";
            }
            return "Error: bad exchange name.";
        }

        [ScriptFunction("bmap", "Register a mapping for the device Id pattern to the shell Id or Title, Babel message exchange and port number.\n"
            + "If no args given, shows current mappings.\n"
            + "Returns empty string or error message.",
            typeof(Jint.Delegates.Func<String, String, String, Int32, String>),
            "Device Id pattern", "Id or Title of shell.", "Name of exchange.", "NetIf number for device.")]
        public static string BMap(string deviceIdPattern = "", string shellIdOrTitle = "", string exchangeName = "", int netIfIndex = -1) {
            if (netIfIndex < 0) {
                string s = "bmap:\n";
                bool isFirst = true;
                foreach (KeyValuePair<string, Tuple<string, string, byte>> map in Commander.ExchangeMapping) {
                    if (!isFirst) s += "\n";
                    else isFirst = false;
                    s += "{" + map.Key + ":(" + map.Value.Item1 + "," + map.Value.Item2 + "," + map.Value.Item3 + ")}";
                }
                return s;
            }
            if (!String.IsNullOrWhiteSpace(shellIdOrTitle) && !String.IsNullOrWhiteSpace(exchangeName) && !String.IsNullOrWhiteSpace(deviceIdPattern)) {
                Commander.ExchangeMapping[deviceIdPattern] = new Tuple<string, string, byte>(shellIdOrTitle, exchangeName, (byte)netIfIndex);
                return "";
            }
            return "Error: bad parameter.";
        }

        [ScriptFunction("bmapclear", "Clear mappings from Babel message exchange.\n"
            + "Returns empty.",
            typeof(Jint.Delegates.Func<String, String, int, String>),
            "Id of shell.", "Name of exchange.", "NetIf number.")]
        public static string BMapClear(string shellId = "", string exchangeName = "", int netIfIndex = -1) {
            List<string> removals = new List<string>();
            foreach (KeyValuePair<string, Tuple<string, string, byte>> map in Commander.ExchangeMapping) {
                if (String.IsNullOrWhiteSpace(shellId) || map.Value.Item1.Equals(shellId)) {
                    if (String.IsNullOrWhiteSpace(exchangeName) || map.Value.Item2.Equals(exchangeName)) {
                        if (netIfIndex < 0 || (map.Value.Item3 == netIfIndex)) {
                            removals.Add(map.Key);
                        }
                    }
                }
            }
            foreach (string s in removals) {
                Commander.ExchangeMapping.Remove(s);
            }
            return "";
        }

        [ScriptFunction("bnetif", "Sets the default netIf number.",
            typeof(Jint.Delegates.Func<Int32,Int32>), "NetIf Number, or negative to return current.")]
        public static int SetDefaultPortNumber(int netIfIndex = -1) {
            if ((netIfIndex >= 0) && (netIfIndex < 16))
                Commander.DefaultNetIfNumber = netIfIndex;
            return Commander.DefaultNetIfNumber;
        }

        [ScriptFunction("tput", "Add or update an entry in device parameter table for the exchange.\n"
            + "Returns empty string or error message.",
            typeof(Jint.Delegates.Func<String, Int32, Int32, Int32, Int32, Int32, Object, Object, Object,
                Object, Object,
                String, String>),
            "Name of exchange.", "Parameter index.", "Parameter category.", "Parameter representation kind.",
            "Parameter size.", "Parameter storage flags.",
            "Min value.", "Max value.", "Default value.",
            "Ram value.", "EEprom value.",
            "Parameter name."
            )]
        public static string TPut(string exchangeName,
                int parameterIndex, int parameterCategory, int parameterVarKind,
                int parameterSize, int parameterStorageFlags,
                Object minValue, Object maxValue, Object defaultValue,
                Object ramValue, Object eepromValue, string parameterName) {
            if (!String.IsNullOrWhiteSpace(exchangeName)) {
                ProtocolHub h = null;
                Commander.Exchanges.TryGetValue(exchangeName, out h);
                if (h == null) return "Error: exchange not found:" + exchangeName + ".";
                string result = h.Exchange.ParameterTable.Put(parameterIndex,
                    parameterCategory, parameterVarKind,
                    parameterSize, parameterStorageFlags,
                    minValue, maxValue, defaultValue,
                    ramValue, eepromValue, parameterName);
                if (result == null) return "";
                return result;
            }
            return "Error: bad exchange name.";
        }

        [ScriptFunction("tputf", "Add or update a field in device parameter table for the exchange.\n"
            + "Returns empty string or error message.",
            typeof(Jint.Delegates.Func<String, String, String, Object, String>),
                "Exchange name.", "Parameter name or index.", "Field name or Id.", "Value.")]
        public static string TPutField(string exchangeName,
                string parameterNameOrIndex, string fieldNameOrId, Object value) {
            if (!String.IsNullOrWhiteSpace(exchangeName)) {
                ProtocolHub h = null;
                Commander.Exchanges.TryGetValue(exchangeName, out h);
                if (h == null) return "Error: exchange not found:" + exchangeName + ".";
                string result = h.Exchange.ParameterTable.PutField(parameterNameOrIndex, fieldNameOrId, value);
                if (result == null) return "";
                return result;
            }
            return "Error: bad exchange name.";
        }

        [ScriptFunction("tget", "Get an entry from device parameter table for the exchange.\n"
            + "Returns parameters as a string tuple or error message.",
            typeof(Jint.Delegates.Func<String, String, bool, String>),
        "Exchange name.", "Parameter name or index.", "Output in command form if true.")]
        public static string TGetParameters(string exchangeName, string parameterNameOrIndex = "*", bool asCmds = false) {
            if (!String.IsNullOrWhiteSpace(exchangeName)) {
                string result = null;
                ProtocolHub h = null;
                Commander.Exchanges.TryGetValue(exchangeName, out h);
                if (h == null) return "Error: exchange not found:" + exchangeName + ".";
                if (String.IsNullOrWhiteSpace(parameterNameOrIndex) || parameterNameOrIndex.Equals("*")) {
                    HashSet<DeviceParameter> s = h.Exchange.ParameterTable.GetEntries();
                    foreach (DeviceParameter d in s) {
                        string v = d.ToString();
                        if (asCmds) v = "tput(exname," + v + ");";
                        result += v + "\n";
                    }
                } else {
                    result = h.Exchange.ParameterTable.GetParameters(parameterNameOrIndex);
                    if (result == null) return "Error: entry not found.";
                    if (asCmds) return "tput(exname," + result + ");";
                }
                return result;
            }
            return "Error: bad exchange name.";
        }

        [ScriptFunction("tgetf", "Get a field from device parameter table for the exchange.\n"
            + "Returns value or error message.",
            typeof(Jint.Delegates.Func<String, String, String, Object>),
                "Exchange name.", "Parameter name or index.", "Field name or Id.")]
        public static object TGetField(string exchangeName, string parameterNameOrIndex, string fieldNameOrId) {
            if (!String.IsNullOrWhiteSpace(exchangeName)) {
                ProtocolHub h = null;
                Commander.Exchanges.TryGetValue(exchangeName, out h);
                if (h == null) return "Error: exchange not found:" + exchangeName + ".";
                object result = h.Exchange.ParameterTable.GetField(parameterNameOrIndex, fieldNameOrId);
                if (result == null) return "Error: field not found.";
                return result;
            }
            return "Error: bad exchange name.";
        }

        [ScriptFunction("bytes", "Create a byte array.\n"
            + "Returns byte[].",
            typeof(Jint.Delegates.Func<Int32, byte[]>),
            "Size of byte array to create, from 0 to 10000.")]
        public static byte[] BCreateByteArray(int len = 64) {
            if (len < 0) len = 0;
            if (len > 10000) len = 10000;
            return new byte[len];
        }

        [ScriptFunction("bcreate", "Create a Babel command message.\n"
            + "Returns message.",
            typeof(Jint.Delegates.Func<String, bool, Int32, Int32, Int32, Int32, Int32, Int32, Int32,
                byte[], Object>),
                "Exchange name.", "Verified.", "Post netif.", "Cmd", "Receiver",
                "Sender", "flagsRS", "Ident", "Data Len", "Data Array")]
        public static Object BCreate(string exchangeName, bool verified,
                int postNetIfIndex, int cmd, int receiver,
                int sender, int flagsRS, int ident = ProtocolConstants.IDENT_USER, int dataLen = 0, byte[] dataAry = null) {
            if (!String.IsNullOrWhiteSpace(exchangeName)) {
                ProtocolHub h = null;
                Commander.Exchanges.TryGetValue(exchangeName, out h);
                if (h == null) return "Error: exchange not found:" + exchangeName + ".";
                object result = BabelMessage.CreateCommandMessage(h.Exchange, verified,
                        Router.RouterAction.PostToNetIf,
                     (byte)postNetIfIndex, (byte)cmd, (ushort)receiver, 
                     (ushort)sender, (byte)flagsRS, (byte)ident, (byte)dataLen, (byte)0, dataAry);
                if (result == null) return "Error: unable to create message.";
                return result;
            }
            return "Error: bad exchange name.";
        }

        [ScriptFunction("bput", "Submit a Babel message.\n"
            + "Returns true if message submitted.",
            typeof(Jint.Delegates.Func<BabelMessage, bool>), "Babel message.")]
        public static bool BPut(BabelMessage message) {
            if (message != null) {
                return message.Exchange.SubmitMessage(message, Commander, false, 1);
            }
            return false;
        }

        [ScriptFunction("bget", "Get message from queue.\n"
            + "Returns Babel message, or null if error. Waits if queue is empty.",
            typeof(Jint.Delegates.Func<String, Int32, BabelMessage>), "Exchange name.", "Queue ident.")]
        public static BabelMessage BGetMessage(String exchangeName, int id = ProtocolConstants.IDENT_USER) {
            if (!String.IsNullOrWhiteSpace(exchangeName)) {
                ProtocolHub h = null;
                Commander.Exchanges.TryGetValue(exchangeName, out h);
                if (h == null) return null;
                return h.GetMessageFromQueue(id);
            }
            return null;
        }

        [ScriptFunction("bavailable", "Number of messages in incoming message queue.\n"
            + "Returns number of messages in incoming message queue, or -1 if error.",
            typeof(Jint.Delegates.Func<String, Int32, Int32>), "Exchange name.", "Queue ident.")]
        public static int BAvailable(String exchangeName, int id = ProtocolConstants.IDENT_USER) {
            if (!String.IsNullOrWhiteSpace(exchangeName)) {
                ProtocolHub h = null;
                Commander.Exchanges.TryGetValue(exchangeName, out h);
                if (h != null)
                    return h.GetQueueLength(id);
            }
            return -1;
        }

        [ScriptFunction("bwaiting", "Number of messages waiting in outgoing queue.\n"
            + "Returns number of messages in outgoing message queue, or -1 if error.",
            typeof(Jint.Delegates.Func<String, Int32>), "Exchange name.")]
        public static int BWaiting(String exchangeName) {
            if (!String.IsNullOrWhiteSpace(exchangeName)) {
                ProtocolHub h = null;
                Commander.Exchanges.TryGetValue(exchangeName, out h);
                if (h != null)
                    return h.Exchange.OutgoingQueueSize();
            }
            return -1;
        }

        [ScriptFunction("bping", "Ping exchange netIf.\n"
            + "Returns empty string, or error message.",
            typeof(Jint.Delegates.Func<String, Int32, String>), "Exchange name.", "NetIf number.")]
        public static string BPing(String exchangeName, int netIfIndex = ProtocolConstants.NETIF_USER_BASE) {
            if (!String.IsNullOrWhiteSpace(exchangeName)) {
                ProtocolHub h = null;
                Commander.Exchanges.TryGetValue(exchangeName, out h);
                if (h != null) {
                    h.Exchange.Manager.PingNetIf((byte)netIfIndex);
                    return "";
                }
            }
            return "Error: bad exchange name.";
        }

        [ScriptFunction("bresetnetif", "Reset exchange netIf.\n"
            + "Returns empty string, or error message.",
            typeof(Jint.Delegates.Func<String, Int32, String>), "Exchange name.", "NetIf number.")]
        public static string BResetNetIf(String exchangeName, int netIfIndex = ProtocolConstants.NETIF_USER_BASE) {
            if (!String.IsNullOrWhiteSpace(exchangeName)) {
                ProtocolHub h = null;
                Commander.Exchanges.TryGetValue(exchangeName, out h);
                if (h != null) {
                    h.Exchange.Manager.ResetDriver((byte)netIfIndex);
                    return "";
                }
            }
            return "Error: bad exchange name.";
        }

        [ScriptFunction("bresetdevice", "Submit reset device cmd message to device attached to netIf.\n"
            + "Returns empty string, or error message.",
            typeof(Jint.Delegates.Func<String, Int32, Int32, String>), "Exchange name.", "Posting netIf number.", "Device address.")]
        public static string BResetDevice(String exchangeName, int postNetIfIndex = ProtocolConstants.NETIF_USER_BASE, int addrs = ProtocolConstants.ADRS_LOCAL) {
            if (!String.IsNullOrWhiteSpace(exchangeName)) {
                ProtocolHub h = null;
                Commander.Exchanges.TryGetValue(exchangeName, out h);
                if (h != null) {
                    BabelMessage message = BabelMessage.CreateCommandMessage(
                        h.Exchange, false,
                        Router.RouterAction.PostToNetIf,
                        (byte)postNetIfIndex, ProtocolConstants.MEDIATOR_DEVICE_RESET,
                        (ushort)addrs, 
                        ProtocolConstants.ADRS_LOCAL, 0,
                        ProtocolConstants.IDENT_USER, (byte)0, (byte)0, null
                    );
                    if (!message.Exchange.SubmitMessage(message, Commander, false, 1)) {
                        return "Error: unable to submit message.";
                    }
                    return "";
                }
            }
            return "Error: bad exchange name.";
        }

        [ScriptFunction("berasedevice", "Submit erase device cmd message to device attached to port.\n"
            + "Returns empty string, or error message.",
            typeof(Jint.Delegates.Func<String, Int32, Int32, String>), "Exchange name.", "Posting port number.", "Device address.")]
        public static string BEraseDevice(String exchangeName, int postNetIfIndex = ProtocolConstants.NETIF_USER_BASE, int addrs = ProtocolConstants.ADRS_LOCAL) {
            if (!String.IsNullOrWhiteSpace(exchangeName)) {
                ProtocolHub h = null;
                Commander.Exchanges.TryGetValue(exchangeName, out h);
                if (h != null) {
                    BabelMessage message = BabelMessage.CreateCommandMessage(
                        h.Exchange, false,
                        Router.RouterAction.PostToNetIf,
                        (byte)postNetIfIndex, ProtocolConstants.MEDIATOR_DEVICE_ERASE,
                        (ushort)addrs, ProtocolConstants.ADRS_LOCAL, 0,
                        ProtocolConstants.IDENT_USER, (byte)0, (byte)0, null
                    );
                    if (!message.Exchange.SubmitMessage(message, Commander, false, 1)) {
                        return "Error: unable to submit message.";
                    }
                    return "";
                }
            }
            return "Error: bad exchange name.";
        }

        [ScriptFunction("bstatus", "Submit status message for device attached to port.\n"
            + "Returns empty string, or error message. Poll IDENT_USER incoming queue for result.",
            typeof(Jint.Delegates.Func<String, Int32, Int32, String>), "Exchange name.", "Posting port number.", "Device address.")]
        public static string BStatus(String exchangeName, int postNetIfIndex = ProtocolConstants.NETIF_USER_BASE, int addrs = ProtocolConstants.ADRS_LOCAL) {
            if (!String.IsNullOrWhiteSpace(exchangeName)) {
                ProtocolHub h = null;
                Commander.Exchanges.TryGetValue(exchangeName, out h);
                if (h != null) {
                    BabelMessage message = BabelMessage.CreateCommandMessage(
                        h.Exchange, false,
                        Router.RouterAction.PostToNetIf,
                        (byte)postNetIfIndex, ProtocolConstants.MEDIATOR_DEVICE_STATUS,
                        (ushort)addrs, ProtocolConstants.ADRS_LOCAL, 0,
                        ProtocolConstants.IDENT_USER, (byte)0, (byte)0, null
                    );
                    
                    if (!message.Exchange.SubmitMessage(message, Commander, false, 1)) {
                        return "Error: unable to submit message.";
                    }
                    return "";
                }
            }
            return "Error: bad exchange name.";
        }

        [ScriptFunction("bticks", "Submit ticker message for device attached to port.\n"
            + "Returns empty string, or error message. Poll IDENT_USER incoming queue for result.",
            typeof(Jint.Delegates.Func<String, Int32, Int32, String>), "Exchange name.", "Posting port number.", "Device address.")]
        public static string BTicker(String exchangeName, int postNetIfIndex = ProtocolConstants.NETIF_USER_BASE, int addrs = ProtocolConstants.ADRS_LOCAL) {
            if (!String.IsNullOrWhiteSpace(exchangeName)) {
                ProtocolHub h = null;
                Commander.Exchanges.TryGetValue(exchangeName, out h);
                if (h != null) {
                    BabelMessage message = BabelMessage.CreateCommandMessage(
                        h.Exchange, false,
                        Router.RouterAction.PostToNetIf,
                        (byte)postNetIfIndex, ProtocolConstants.MEDIATOR_DEVICE_TICKER,
                        (ushort)addrs, ProtocolConstants.ADRS_LOCAL, (byte)0,
                        ProtocolConstants.IDENT_USER, (byte)0, (byte)0, null
                    );
                    if (!message.Exchange.SubmitMessage(message, Commander, false, 1)) {
                        return "Error: unable to submit message.";
                    }
                    return "";
                }
            }
            return "Error: bad exchange name.";
        }

        [ScriptFunction("breadp", "Request device parameter.\n"
            + "Returns empty string, or error message. Poll IDENT_USER incoming queue for result.",
            typeof(Jint.Delegates.Func<String, Int32, String, String, Int32, Int32, String>),
            "Exchange name.", "Page number.", "Parameter name or index.", "Field name (ram,rom,default,range).",
            "Posting netIf number.", "Device address.")]
        public static string BReadP(String exchangeName, int pageNum, string parameterNameOrIndex, string fieldName = "ram",
                int postNetIfIndex = ProtocolConstants.NETIF_USER_BASE, int addrs = ProtocolConstants.ADRS_LOCAL) {
            if (!String.IsNullOrWhiteSpace(exchangeName)) {
                ProtocolHub h = null;
                int paramIndex = DeviceParameterTable.ConvertToIndex(parameterNameOrIndex);
                if (paramIndex < 0) {
                    if (String.IsNullOrWhiteSpace(parameterNameOrIndex) || (parameterNameOrIndex.Length > ProtocolConstants.MAX_PARAMETER_NAME_SIZE))
                        return "Error: parameter name not valid.";
                }
                Commander.Exchanges.TryGetValue(exchangeName, out h);
                if (h != null) {
                    byte[] dataArray;
                    if (paramIndex >= 0) {
                        dataArray = new byte[5];
                        dataArray[0] = 1;
                        dataArray[1] = (byte)DeviceParameter.GetReadWriteVarFlag(fieldName);
                        dataArray[2] = (byte)pageNum;
                        dataArray[3] = 1;
                        dataArray[4] = (byte)paramIndex;
                    } else {
                        int len = parameterNameOrIndex.Length;
                        dataArray = new byte[3 + len];
                        dataArray[0] = 1;
                        dataArray[1] = (byte)(ProtocolConstants.MEDIATOR_DEVICE_RWV_FLAGS_BY_NAME | DeviceParameter.GetReadWriteVarFlag(fieldName));
                        dataArray[2] = (byte)pageNum;
                        Primitives.SetArrayString(parameterNameOrIndex, dataArray, 3);
                    }
                    BabelMessage message = BabelMessage.CreateCommandMessage(
                        h.Exchange, false,
                        Router.RouterAction.PostToNetIf,
                        (byte)postNetIfIndex, ProtocolConstants.MEDIATOR_DEVICE_READVAR,
                        (ushort)addrs, ProtocolConstants.ADRS_LOCAL, 0,
                        ProtocolConstants.IDENT_USER, (byte)dataArray.Length, (byte)0, dataArray
                    );
                    if (!message.Exchange.SubmitMessage(message, Commander, false, 1)) {
                        return "Error: unable to submit message.";
                    }
                    return "";
                }
            }
            return "Error: bad exchange name.";
        }

        [ScriptFunction("bwritep", "Write to device parameter.\n"
            + "Returns empty string, or error message. Poll IDENT_USER incoming queue for result.",
            typeof(Jint.Delegates.Func<String, Int32, String, String, Int32, String, Int32, Int32, String>),
            "Exchange name.", "Page number.", "Parameter index.", "Value", "Value size in bytes, 0 for strings.",
            "Field name (ram,rom,default,range).",
            "Posting netIf number.", "Device address.")]
        public static string BWriteP(String exchangeName, int pageNum, string parameterIndex, string value,
                int valSize = 2, string fieldName = "ram",
                int postNetIfIndex = ProtocolConstants.NETIF_USER_BASE, int addrs = ProtocolConstants.ADRS_LOCAL) {
            if (!String.IsNullOrWhiteSpace(exchangeName)) {
                ProtocolHub h = null; bool isString = (valSize == 0);
                int paramIndex = DeviceParameterTable.ConvertToIndex(parameterIndex);
                if (paramIndex < 0) return "Error: using parameter names when writing not allowed.";
                if (isString) {
                    valSize = 1 + value.Length;
                } else {
                    if (valSize < 1 || valSize > 30) return "Error: value size out of range [1..30].";
                }
                Commander.Exchanges.TryGetValue(exchangeName, out h);
                if (h != null) {
                    byte[] dataArray = new byte[5 + valSize];
                    dataArray[0] = 1;
                    dataArray[1] = (byte)DeviceParameter.GetReadWriteVarFlag(fieldName);
                    dataArray[2] = (byte)pageNum;
                    dataArray[3] = 1;
                    dataArray[4] = (byte)paramIndex;
                    if (isString) {
                        Primitives.SetArrayStringValue(value, dataArray, 4);
                    } else {
                        if (value.Contains(".") && valSize == 4) {
                            Double d = Convert.ToDouble(value);
                            Primitives.SetArrayFloat((float)d, dataArray, 5, valSize);
                        } else {
                            Primitives.SetArrayValue(Convert.ToInt64(value), dataArray, 5, valSize);
                        }
                    }
                    BabelMessage message = BabelMessage.CreateCommandMessage(
                        h.Exchange, false,
                        Router.RouterAction.PostToNetIf,
                        (byte)postNetIfIndex, ProtocolConstants.MEDIATOR_DEVICE_WRITEVAR,
                        (ushort)addrs, ProtocolConstants.ADRS_LOCAL, (byte)0,
                        ProtocolConstants.IDENT_USER, (byte)dataArray.Length, (byte)0, dataArray
                    );
                    if (!message.Exchange.SubmitMessage(message, Commander, false, 1)) {
                        return "Error: unable to submit message.";
                    }
                    return "";
                }
            }
            return "Error: bad exchange name.";
        }

        [ScriptFunction("bisoreadp", "Isochronous request for device parameter(s).\n"
            + "Returns empty string, or error message.\n"
            + "Poll Message Ident incoming queue for results.\n"
            + "Note, Ticker is automatically included as Parameter 0.",
            typeof(Jint.Delegates.Func<String, Int32, Int32, Int32, Int32, Int32, Int32, String, Int32, Int32,
                bool, Int32, Int32, Int32, Int32, String>),
            "Exchange name.",
            "Page number.",
            "Parameter 1 index.", "Parameter 2 index.", "Parameter 3 index.", "Parameter 4 index.", "Parameter 5 index.",
            "Field name (ram,rom,default,range).",
            "Millisecond interval (0=cancel).", "Repeat count (0=forever).",
            "Monitor or Read only.",
            "Iso id.",
            "Message user ident.",
            "Posting netIf number.",
            "Device address.")]
        public static string BIsoReadP(String exchangeName,
                int pageNum,
                int param1Index, int param2Index = -1, int param3Index = -1, int param4Index = -1, int param5Index = -1,
                string fieldName = "ram",
                int milliInterval = 100, int repeatCount = 0,
                bool isMonitor = true,
                int isoId = 1,
                int ident = ProtocolConstants.IDENT_USER,
                int postNetIfNo = ProtocolConstants.NETIF_USER_BASE,
                int addrs = ProtocolConstants.ADRS_LOCAL) {
            if (!String.IsNullOrWhiteSpace(exchangeName)) {
                ProtocolHub h = null;
                int numParams = 1;
                if (param1Index < 0) {
                    return "Error: only parameter indexes valid for isochronous reads.";
                }
                if (param2Index >= 0) {
                    ++numParams;
                    if (param3Index >= 0) {
                        ++numParams;
                        if (param4Index >= 0) {
                            ++numParams;
                            if (param5Index >= 0)
                                ++numParams;
                        }
                    }
                }
                Commander.Exchanges.TryGetValue(exchangeName, out h);
                if (h != null) {
                    byte[] dataArray = new byte[10 + numParams];
                    dataArray[0] = (byte)isoId;
                    dataArray[1] = (byte)(milliInterval & 0xff);
                    dataArray[2] = (byte)((milliInterval >> 8) & 0xff);
                    dataArray[3] = (byte)(repeatCount & 0xff);
                    dataArray[4] = (byte)((repeatCount >> 8) & 0xff);
                    dataArray[5] = 1; // ReadId.
                    dataArray[6] = (byte)(DeviceParameter.GetReadWriteVarFlag(fieldName));
                    dataArray[7] = (byte)pageNum;
                    dataArray[8] = (byte)(1 + numParams);
                    dataArray[9] = ProtocolConstants.PARAMETER_TABLE_INDEX_TICKER;
                    dataArray[10] = (byte)param1Index;
                    if (numParams > 1) dataArray[11] = (byte)param2Index;
                    if (numParams > 2) dataArray[12] = (byte)param3Index;
                    if (numParams > 3) dataArray[13] = (byte)param4Index;
                    if (numParams > 4) dataArray[14] = (byte)param5Index;

                    IsoRead ir = new IsoRead(dataArray, exchangeName, isMonitor, isoId, ident, postNetIfNo, addrs, milliInterval, repeatCount);
                    Commander.UpdateIsoReads(ir);
                    if (!ir.PostMessage(false)) {
                        return "Error: unable to submit message.";
                    }
                    return "";
                }
            }
            return "Error: bad exchange name.";
        }

        [ScriptFunction("bparse", "Parse a read var message into readable parameters.\n"
            + "Returns Babel message in readable form, or error message.",
            typeof(Jint.Delegates.Func<BabelMessage, String>), "Babel message.")]
        public static string BParseMessage(BabelMessage message) {
            if (message != null) {
                // Check for read var message kind.
                if (message.Cmd == ProtocolConstants.MEDIATOR_DEVICE_READVAR
                    || message.Cmd == ProtocolConstants.MEDIATOR_DEVICE_ISOVAR
                    || message.Cmd == ProtocolConstants.MEDIATOR_DEVICE_ISOMONVAR) {
                    Dictionary<int, DeviceParameter> indexTable = new Dictionary<int, DeviceParameter>();
                    int numProcessed = ParameterManager.ProcessReadVarMessage(message, null, indexTable, null, null);
                    if (indexTable.Count > 0) {
                        string s = "";
                        foreach (DeviceParameter d in indexTable.Values) {
                            if (s.Length > 0) s += "\n";
                            s += d.ToString();
                        }
                        return s;
                    }
                    return message.ReadVarMessageToString();
                }
                return message.ToString();
            }
            return "null message";
        }

        // Checks if any IsoRead commands need to be re-posted.
        public void IsoReadsHandler() {
            while (!IsClosing) {
                try {
                    long now = DateTime.UtcNow.Ticks - IsoRead.UpdateInterval;
                    lock (IsoReadsLock) {
                        foreach (IsoRead r in Commander.IsoReads.Values) {
                            if (r != null && r.GetTime() < now) {
                                r.PostMessage(false);
                            }
                        }
                    }
                    Thread.Sleep(5 * 1000);
                } catch (ThreadInterruptedException) {
                    break;
                } catch (Exception e) {
                    // ignore.
                    Log.d(TAG, "IsoReadsHandlerThread exception:" + e.Message);
                }
            }
            Log.d(TAG, "IsoReadsHandlerThread exiting.");
        }
    }
}
