using System;
using System.IO;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Remoting;
using System.Xml;

namespace Babel.Core {

    /// <summary>
    /// Manages a cache of data vectors.
    /// </summary>
    public class DataCache: IComparer<List<double>> {

        public const String TAG = "DataCache";

        protected int CacheSize;
        protected int CacheOffset;
        protected bool IsClosing;
        protected bool HasTimeData;
        protected bool UseRelativeTimeBase;
        protected double TimeIndex;
        protected double TimeBase;
        protected string OwnerName;
        public List<List<double>> PointList;
        public List<double> MinValues;
        public List<double> MaxValues;
        public List<double> RangeValues;
        protected Thread Task;
        public Object DataAdded;

        private void init() {
            HasTimeData = true;
            UseRelativeTimeBase = true;
            CacheSize = 10000;
            CacheOffset = 0;
            IsClosing = false;
            TimeIndex = 0.0;
            TimeBase = 0.0;
            OwnerName = "System";
            PointList = new List<List<double>>();
            MinValues = new List<double>();
            MaxValues = new List<double>();
            RangeValues = new List<double>();
            DataAdded = new Object();
            Task = null;
        }

        public DataCache(string ownerName, bool useThread=true, bool hasTimeData = true, bool useRelativeTimeBase = true, int cacheSize = 10000) {
            init();
            OwnerName = ownerName;
            HasTimeData = hasTimeData;
            UseRelativeTimeBase = useRelativeTimeBase;
            CacheSize = cacheSize;
            SetupThread(useThread);
        }

        public DataCache(XmlNode node) {
            init();
            XmlNode c = Project.GetChildNode(node, "datacache");
            if (c != null) {
                OwnerName = Project.GetNodeAttributeValue(c, "ownername", "System");
                HasTimeData = Convert.ToBoolean(Project.GetNodeAttributeValue(c, "hastimedata", "true"));
                UseRelativeTimeBase = Convert.ToBoolean(Project.GetNodeAttributeValue(c, "userelativetimebase", "true"));
                CacheSize = Convert.ToInt32(Project.GetNodeAttributeValue(c, "cachesize", "10000"));
                bool useThread = Convert.ToBoolean(Project.GetNodeAttributeValue(c, "userelativetimebase", "true"));
                SetupThread(useThread);
            }
        }

        public static DataCache Deserializer(XmlNode node) {
            DataCache p = null;
            XmlNode c = Project.GetChildNode(node, "datacache");
            if (c != null) {
                string cacheTypeName = Project.GetNodeAttributeValue(c, "typename", "");
                string assemblyName = Project.GetNodeAttributeValue(c, "assemblyname", "");
                if (!String.IsNullOrWhiteSpace(cacheTypeName)
                    && !String.IsNullOrWhiteSpace(assemblyName)) {
                    Assembly a = Modules.FindAssembly(assemblyName);
                    if (a != null) {
                        Object[] args = { node };
                        Object obj = a.CreateInstance(cacheTypeName, false, 0, null, args, null, null);
                        if (obj != null) {
                            p = obj as DataCache;
                        }
                    }
                }
            }
            return p;
        }

        public virtual void Serializer(XmlNode node, bool isSerialize) {
            XmlNode c = Project.GetChildNode(node, "datacache");
            if (c == null) return;
            if (isSerialize) {
                string typename = this.GetType().FullName;
                string assemblyName = this.GetType().Assembly.FullName;
                Project.SetNodeAttributeValue(c, "typename",typename);
                Project.SetNodeAttributeValue(c, "assemblyname", assemblyName);
            } else {
                // Does nothing.
            }
        }

        protected void SetupThread(bool useThread = true) {
            if (useThread) {
                Task = new Thread(new ThreadStart(Run));
                Task.Name = "CacheServiceThread:" + OwnerName;
                Task.Priority = ThreadPriority.Normal; // XXX was AboveNormal.
            }
        }

        public void ResetMinMax() {
            MinValues.Clear();
            MaxValues.Clear();
            RangeValues.Clear();
        }

        public void UpdateMinMax(List<double> p) {
            int numCoordinates = p.Count;
            if (MinValues.Count == 0) {
                for (int j = 0; j < numCoordinates; j++) {
                    MinValues.Add(p[j]);
                    MaxValues.Add(p[j]);
                    RangeValues.Add(1.0);
                }
            } else {
                for (int j = 0; j < numCoordinates; j++) {
                    try {
                        double d = p[j];
                        if (!Double.IsNaN(d)) {
                            if (d < MinValues[j]) {
                                MinValues[j] = d;
                            } else if (d > MaxValues[j]) {
                                MaxValues[j] = d;
                            } else continue;
                        } else continue;
                    } catch (Exception) { }
                    double r = MaxValues[j] - MinValues[j];
                    if (r == 0.0) r = 1.0;
                    RangeValues[j] = r;
                }
            }
        }

        public void RefreshMinMax() {
            lock (PointList) 
            {
                ResetMinMax();
                int n = PointList.Count;
                if (n > 0) {
                    int numCoordinates = PointList[0].Count;
                    for (int j = 0; j < numCoordinates; j++) {
                        bool isFirst = true;
                        double minValue = 0.0, maxValue = 0.0;
                        for (int k = 0; k < n; k++) {
                            try {
                                double d = PointList[k][j];
                                if (!Double.IsNaN(d)) {
                                    if (isFirst) {
                                        isFirst = false;
                                        minValue = maxValue = d;
                                    } else {
                                        if (d < minValue) minValue = d;
                                        else if (d > maxValue) maxValue = d;
                                    }
                                }
                            } catch (Exception) { }
                        }
                        double r = maxValue - minValue;
                        if (r == 0.0) r = 1.0;
                        MinValues.Add(minValue);
                        MaxValues.Add(maxValue);
                        RangeValues.Add(r);
                    }
                }
            }
        }

        public virtual void Reset() {
            lock (PointList) {
                CacheOffset = 0;
                TimeIndex = 0.0;
                TimeBase = 0.0;
                PointList.Clear();
                ResetMinMax();
            }
        }

        public virtual void Close() {
            if (!IsClosing) {
                IsClosing = true;
                if (Task != null) {
                    Primitives.Interrupt(Task);
                    Task = null;
                }
                Reset();
            }
        }

        public int RelativeCount {
            get {
                lock (PointList) 
                {
                    return CacheOffset + PointList.Count;
                }
            }
        }

        public int AbsoluteCount {
            get {
                lock (PointList) 
                {
                    return PointList.Count;
                }
            }
        }

        public List<double> RelativeGetAt(int cacheRelativeIndex) {
            lock (PointList) 
            {
                int index = cacheRelativeIndex - CacheOffset;
                if (index >= 0 && index < PointList.Count) {
                    return PointList[index];
                }
            }
            return null;
        }

        public List<double> AbsoluteGetAt(int cacheAbsoluteIndex) {
            lock (PointList) 
            {
                if (cacheAbsoluteIndex >= 0 && cacheAbsoluteIndex < PointList.Count) {
                    return PointList[cacheAbsoluteIndex];
                }
            }
            return null;
        }

        public void AbsoluteInsertAt(int cacheAbsoluteIndex, List<double> d) {
            lock (PointList) {
                if (d.Count > 0) {
                    if (cacheAbsoluteIndex < 0) cacheAbsoluteIndex = 0;
                    if (cacheAbsoluteIndex >= PointList.Count)
                        PointList.Add(d);
                    else
                        PointList.Insert(cacheAbsoluteIndex, d);
                }
            }
        }

        public void RelativeInsertAt(int cacheRelativeIndex,List<double> d) {
            lock (PointList) 
            {
                if (d.Count > 0) {
                    int index = cacheRelativeIndex - CacheOffset;
                    if (index < 0) index = 0;
                    if (index >= PointList.Count)
                        PointList.Add(d);
                    else
                        PointList.Insert(index, d);
                }
            }
        }

        public void WaitForData() {
            lock (DataAdded) {
                Monitor.Wait(DataAdded);
            }
        }

        // For each point, zero elements starting at index.
        // When zeroSpacing is non-zero, repeats zeroing at next element at Index+Spacing etc.
        public void ZeroElements(int zeroIndex, int zeroSpacing = 0) {
            int Count = PointList.Count;
            if (Count > 0) {
                for (int k = 0; k < Count; k++) {
                    List<double> p = PointList[k];
                    int j = zeroIndex;
                    while (j < p.Count) {
                        p[j] = Double.NaN; //was 0.0;
                        if (zeroSpacing == 0) break;
                        j += zeroSpacing;
                    }
                }
            }
        }

        public void AddPoint(List<double> p) {
            if (p != null && p.Count>0) {
                lock (PointList) 
                {
                    if (!HasTimeData) {
                        p.Insert(0, ++TimeIndex);
                    } else if (UseRelativeTimeBase && p.Count>0) {
                        if (PointList.Count == 0) TimeBase = p[0];
                        if (p[0] >= TimeBase) {
                            p[0] -= TimeBase;
                        }
                    }
                    PointList.Add(p);
                    UpdateMinMax(p);
                    if (CacheSize!=0 && PointList.Count > CacheSize) {
                        PointList.RemoveAt(0);
                        ++CacheOffset;
                    }
                    lock (DataAdded) {
                        Monitor.PulseAll(DataAdded);
                    }
                }
            }
        }

        // Deep copy cache onto this cache.
        // Reset first if necessary otherwise cache is appended.
        public void DeepCopy(DataCache d) {
            if (d != null) {
                List<List<double>> c = d.PointList;
                int newCount = c.Count;
                for (int k = 0; k < newCount; k++) {
                    PointList.Add(new List<double>(c[k]));
                }
            }
        }

        public int Compare(List<double> x, List<double> y) {
            try {
                double d = x[0] - y[0];
                if (d > 0.0) return 1;
                if (d < 0.0) return -1;
                return 0;
            } catch (Exception) {
            }
            return -1;
        }

        // Returns a relative position.
        public int FindRelativeXCoordinateInCache(double x) {
            List<double> tmp = new List<double>(4);
            tmp.Add(x);
            lock (PointList) 
            {
                int i = PointList.BinarySearch(tmp, this);
                if (i < 0) return i - CacheOffset;
                return i + CacheOffset;
            }
        }

        // Find closest point timewise.
        // Returns an absolute position.
        // This may be > Count if x > last point.
        public int FindAbsolutePoint(List<double> p) {      
            lock (PointList) {
                if (p.Count == 0) return -1;
                return PointList.BinarySearch(p, this);
            }
        }

        static int TmpCounter = 0;

        // Dump in text csv format.
        public string DumpToFile(string fileNamePath,bool zeroTimeBase, string header) {
            string filePath = null; double timeBase = 0.0; bool isStart = true;
            // fileNamePath may be a path.
            try {
                String wrkDir = Shell.GetWorkingDir(Shell.CurrentShellId);
                if (String.IsNullOrWhiteSpace(wrkDir)) {
                    wrkDir = Project.GetProjectDir();
                }
                // This will take care of combinging a mixture of absolute & relative paths.
                filePath = Path.Combine(wrkDir, fileNamePath);

            } catch (Exception) {
                filePath = "tmpdc" + TmpCounter++;
            }
            System.IO.StreamWriter file = null;
            try {
                file = new System.IO.StreamWriter(filePath);
            } catch (Exception) {
                return "Error: bad file name:" + filePath;
            }
            try {
                lock (PointList) 
                {
                    if (!String.IsNullOrWhiteSpace(header)) {
                        file.WriteLine('#'+header);
                    }
                    foreach (List<double> p in PointList) {
                        String s = "";
                        bool isFirst = true;
                        if (isStart) {
                            isStart = false;
                            timeBase = p[0];
                        }
                        foreach (double r in p) {
                            double v = r;
                            if (isFirst) {
                                isFirst = false;
                                if (zeroTimeBase) v -= timeBase;
                            } else {
                                s += ",";
                            }
                            if(!Double.IsNaN(v))
                                s += v.ToString();
                        }
                        file.WriteLine(s);
                    }
                }
            } catch (Exception) {
            }
            try {
                if (file != null)
                    file.Close();
            } catch (Exception) {
            }
            return filePath;
        }

        char[] DataFileParameterSeparators = { ' ', ',' };

        public string LoadFromFile(string fileName, out string header) {
            string filePath = null;
            int numPoints = 0;
            header = "";
            try {
                String wrkDir = Shell.GetWorkingDir(Shell.CurrentShellId);
                if (String.IsNullOrWhiteSpace(wrkDir)) {
                    wrkDir = Project.GetProjectDir();
                }
                filePath = Path.Combine(wrkDir, fileName);

            } catch (Exception) {              
                return "Error: bad file name:" + filePath;
            }
            System.IO.StreamReader file = null;
            try {
                file = new System.IO.StreamReader(filePath);
            } catch (Exception) {
                return "Error: bad file name:" + filePath;
            }
            try {
                lock (PointList) 
                {
                    for (; ; ) {
                        try {
                            string s = file.ReadLine();
                            if (s == null) break;
                            if (s.StartsWith("#")&&(s.Length>1)) {
                                header = s.Substring(1);
                                continue;
                            }
                            string[] ary=s.Split(DataFileParameterSeparators);
                            if (ary.Length > 0) {
                                List<double> a = new List<double>();
                                for (int k = 0; k < ary.Length; k++) {
                                    double d = 0.0;
                                    if(String.IsNullOrWhiteSpace(ary[k]))
                                        d = Double.NaN;
                                    else 
                                        Double.TryParse(ary[k], out d);
                                    a.Add(d);
                                }
                                ++numPoints;
                                if ((numPoints + 10) > CacheSize) CacheSize = numPoints + 10;
                                AddPoint(a);
                            }
                        } catch (Exception) {
                            break;
                        }
                    }
                }
            } catch (Exception) {
            }
            try {
                if (file != null)
                    file.Close();
            } catch (Exception) {
            }
            return filePath;
        }

        protected virtual void ServiceTask() {
            Thread.Sleep(1000);
        }

        public void Run() {
            while (!IsClosing) {
                try {
                    ServiceTask();
                } catch (ThreadInterruptedException) {
                    break;
                } catch (Exception e) {
                    // ignore.
                    Log.d(TAG, "CacheServiceThread exception:" + e.Message);
                }
            }
        }
    }
}
