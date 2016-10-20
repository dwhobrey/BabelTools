using System;
using System.IO;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Xml;

using Babel.Core;

namespace Babel.BabelProtocol {

    /// <summary>
    /// Manages a cache of data vectors extracted from incoming messages.
    /// </summary>
    public class BabelMessageDataCache : DataCache {

        protected String CacheId;
        protected String ExchangeName;
        protected ProtocolHub Hub;
        protected int MessageId;
        public List<long> OffsetTable;

        private void init() {
            CacheId = (++ProtocolCommands.CacheIdCounter).ToString();
            Hub = null;
            OffsetTable = new List<long>();
            ExchangeName = "";
            MessageId = 0;
        }

        public BabelMessageDataCache(XmlNode node) : base(node) {
            init();
            XmlNode c = Project.GetChildNode(node, "datacache");
            if (c != null) {
                ExchangeName = Project.GetNodeAttributeValue(c, "exchangename", "");
                MessageId = Convert.ToInt32(Project.GetNodeAttributeValue(c, "messageid", "1"));
            }
            ProtocolCommands.AddCache(this);
            if (Task != null) {
                Task.Start();
            }
        }

        public BabelMessageDataCache(String exchangeName, int messageId, bool useThread = true, bool hasTimeData = true, int cacheSize = 10000)
            : base(exchangeName, useThread, hasTimeData, true, cacheSize) {
            init();
            ExchangeName = exchangeName;
            MessageId = messageId;
            ProtocolCommands.AddCache(this);
            if (Task != null) {
                Task.Start();
            }
        }

        public override void Serializer(XmlNode node, bool isSerialize) {
            base.Serializer(node, isSerialize);
            XmlNode c = Project.GetChildNode(node, "datacache");
            if (c == null) return;
            if (isSerialize) {
                Project.SetNodeAttributeValue(c, "exchangename", ExchangeName);
                Project.SetNodeAttributeValue(c, "messageid", MessageId.ToString());
            } else {
                // Does nothing.
            }
        }

        public override string ToString() {
            return CacheId + "," + ExchangeName + "," + MessageId + "," + AbsoluteCount + "/" + CacheSize + ".";
        }

        public string GetCacheId() {
            return CacheId;
        }
        public string GetExchangeName() {
            return ExchangeName;
        }
        public int GetMessageId() {
            return MessageId;
        }

        public override void Close() {
            ProtocolCommands.RemoveCache(GetCacheId());
            base.Close();
        }

        // Waits for incoming data messages to add to cache.
        protected override void ServiceTask() {
            if (Hub == null) {
                ProtocolCommands.Commander.Exchanges.TryGetValue(ExchangeName, out Hub);
            }
            if (Hub != null) {
                if (Hub.Exchange.IsClosingState()) {
                    Hub = null;
                    OffsetTable.Clear();
                } else {
                    BabelMessage b = Hub.GetMessageFromQueue(MessageId);
                    if (b != null) {
                        if (OffsetTable.Count == 0) {
                            ParameterManager.ProcessReadVarMessage(b, null, null, null, null, OffsetTable);
                        }
                        AddPoint(ParameterManager.ProcessReadVarMessageViaOffsets(b, OffsetTable));
                        return;
                    }
                }
            }
            Thread.Sleep(1000);
        }


        [ScriptFunction("bc", "Returns a list of the Babel message data caches.",
            typeof(Jint.Delegates.Func<String>))]
        public static string GetBabelMessageCaches() {
            string report = "bc:\n";
            foreach (BabelMessageDataCache h in ProtocolCommands.Commander.Caches.Values) {
                report += h.ToString() + "\n";
            }
            return report;
        }

        [ScriptFunction("bcachedump", "Dump cache data to file. Returns filePath.",
            typeof(Jint.Delegates.Func<String, String, String>),
            "Cache Id.", "File name.")]
        public static string BCacheDump(string cacheId, string fileName) {
            BabelMessageDataCache d = ProtocolCommands.GetCache(cacheId);
            if (d != null) {
                string header = ""; //TODO: generate header.
                return d.DumpToFile(fileName,true,header);       
            }
            return "Error: unable to get cache.";
        }

        [ScriptFunction("bcacheremove", "Remove cache.",
            typeof(Jint.Delegates.Func<String, String>),
            "Cache Id.")]
        public static string BCacheRemove(string cacheId) {
            if (!String.IsNullOrWhiteSpace(cacheId)) {
                ProtocolCommands.RemoveCache(cacheId);
                return "Cache: " + cacheId + " removed.";
            }
            return "Error: bad cache name.";
        }

        // TODO: commands to close cache.
    }
}
