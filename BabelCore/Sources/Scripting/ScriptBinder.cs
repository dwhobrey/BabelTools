using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Jint;

namespace Babel.Core {

    public class ScriptVariableAttribute : Attribute {
        private string Name;
        private string Description;

        public ScriptVariableAttribute(string name, string description) {
            Name = name;
            Description = description;
        }
        public string GetName() {
            return Name;
        }
        public string GetDescription() {
            return Description;
        }
    }

    public class ScriptFunctionAttribute : Attribute {
        protected bool HasParams;
        protected string Name;
        protected string Description;
        protected string[] ParameterDescriptions;
        protected Type FunctionType;

        public ScriptFunctionAttribute(string name, string description, Type funcType, params string[] paramDescriptions) {
            HasParams = false;
            Name = name;
            Description = description;
            ParameterDescriptions = paramDescriptions;
            FunctionType = funcType;
        }
        public ScriptFunctionAttribute(string name, string description, Type funcType) {
            HasParams = false;
            Name = name;
            Description = description;
            ParameterDescriptions = null;
            FunctionType = funcType;
        }
        public bool GetHasParams() {
            return HasParams;
        }
        public string GetName() {
            return Name;
        }
        public string GetDescription() {
            return Description;
        }
        public Type GetFuncType() {
            return FunctionType;
        }
        public string[] GetParameterDescriptions() {
            return ParameterDescriptions;
        }
    }

    public class ScriptParamsFunctionAttribute : ScriptFunctionAttribute {

        public ScriptParamsFunctionAttribute(string name, string description, Type funcType, params string[] paramDescriptions)
            : base(name, description, funcType, paramDescriptions) {
            HasParams = true;
            Name = name;
            Description = description;
            ParameterDescriptions = paramDescriptions;
            FunctionType = funcType;
        }
        public ScriptParamsFunctionAttribute(string name, string description, Type funcType)
            : base(name, description, funcType) {
            HasParams = true;
            Name = name;
            Description = description;
            ParameterDescriptions = null;
            FunctionType = funcType;
        }
    }

    public class ScriptElementDescriptor {
        private bool IsFunc;
        private bool HasParams;
        private string Name;
        private string Description;
        private string FullDetails;
        private string FuncHeader;
        private object VarRef;
        private Delegate FuncDele;

        public ScriptElementDescriptor(string name, string description, string typeName, object varRef) {
            IsFunc = false;
            HasParams = false;
            Name = name;
            Description = description;
            FuncHeader = typeName + " " + name;
            FullDetails = FuncHeader + "\n " + description;
            VarRef = varRef;
            FuncDele = null;
        }

        public ScriptElementDescriptor(string name, string description, string typeName, bool hasParams, ArrayList funcParams, ArrayList funcParamDescriptions, Delegate funcDelegate) {
            IsFunc = true;
            HasParams = hasParams;
            Name = name;
            Description = description;
            string strFuncBody = "\n";
            string strFuncParams = "";
            Boolean bFirst = true;
            for (int i = 0; i < funcParams.Count; i++) {
                if (!bFirst)
                    strFuncParams += ", ";
                if ((i > 3) && (i % 4 == 0)) strFuncParams += "\n  ";
                strFuncParams += funcParams[i];
                strFuncBody += " " + funcParams[i] + "\t " + funcParamDescriptions[i] + "\n";
                bFirst = false;
            }
            if (HasParams) strFuncParams += ",...";
            FuncHeader = typeName + " " + name + "(" + strFuncParams + ")";
            FullDetails = FuncHeader + "\n " + description + strFuncBody;
            FuncDele = funcDelegate;
            VarRef = null;
        }

        public bool GetIsFunc() { return IsFunc; }

        public string GetName() {
            return Name;
        }

        public string GetDescription() {
            return Description;
        }

        public string GetHeader() {
            return FuncHeader;
        }

        public string GetFullDetails() {
            return FullDetails;
        }

        public Delegate GetDelegate() {
            return FuncDele;
        }

        public object GetVarRef() {
            return VarRef;
        }
    }

    public class ScriptBinder {

        static SortedDictionary<string, ScriptElementDescriptor> ScriptElements = new SortedDictionary<string, ScriptElementDescriptor>();

        public static ScriptElementDescriptor GetScriptElement(string elementName) {
            if (String.IsNullOrWhiteSpace(elementName)) return null;
            ScriptElementDescriptor d = null;
            ScriptElements.TryGetValue(elementName,out d);
            return d;
        }

        public static bool IsFunctionName(string name) {
            ScriptElementDescriptor d = GetScriptElement(name);
            return (d != null) && d.GetIsFunc();
        }

        private static string[] PrunePrefixes = { "System.Collections.", "System." };

        private static string PruneTypeName(string typeName) {
            foreach (string s in PrunePrefixes) {
                if (typeName.StartsWith(s)) return typeName.Substring(s.Length);
            }
            return typeName;
        }

        internal static void ScanTypeForScriptElements(Type targetType) {
            foreach (MethodInfo info in targetType.GetMethods(BindingFlags.Public | BindingFlags.Static)) {
                foreach (Attribute attr in Attribute.GetCustomAttributes(info)) {
                    if ((attr.GetType() == typeof(ScriptFunctionAttribute)) || (attr.GetType() == typeof(ScriptParamsFunctionAttribute))) {
                        ScriptFunctionAttribute scriptAttr = (ScriptFunctionAttribute)attr;
                        ArrayList funcParams = new ArrayList();
                        ArrayList funcParamDescriptions = new ArrayList();
                        bool hasParams = scriptAttr.GetHasParams();
                        string name = scriptAttr.GetName();
                        string[] paramDescriptions = scriptAttr.GetParameterDescriptions();
                        ParameterInfo[] paramInfos = info.GetParameters();
                        int paramLen = paramDescriptions == null ? 0 : paramDescriptions.Length;
                        int infosLen = paramInfos == null ? 0 : paramInfos.Length;
                        if (paramDescriptions != null) {
                            int paramLenDiff = infosLen - paramLen;
                            if (((!hasParams) && (paramLenDiff != 0)) || ((hasParams) && (paramLenDiff < 0))) {
                                Settings.BugReport("Error: function '" + info.Name + "' (exported as '" + name + "') argument number mismatch.\n"
                                    + " Declared " + paramLen + " but requires " + infosLen + ".");
                                break;
                            }
                        }
                        if (hasParams) --paramLen;
                        for (int i = 0; i < paramLen; i++) {
                            funcParams.Add(PruneTypeName(paramInfos[i].ParameterType.ToString()) + " " + paramInfos[i].Name);
                            funcParamDescriptions.Add(paramDescriptions[i]);
                        }
                        if (hasParams) {
                            funcParams.Add("params String[] args");
                            funcParamDescriptions.Add("Optional variable number of args.");
                        }
                        if (ScriptElements.ContainsKey(name)) {
                            ScriptElements.Remove(name);
                        }
                        Delegate d = null;
                        try {
                            d = Delegate.CreateDelegate(scriptAttr.GetFuncType(), null, info);
                        } catch (Exception) {
                            Settings.BugReport("ScriptBinder: Custom command prototype arg mismatch: " + scriptAttr.GetName());
                            d = null;
                        }
                        if (d != null) {
                            ScriptElements.Add(name,
                                new ScriptElementDescriptor(name, scriptAttr.GetDescription(), PruneTypeName(info.ReturnType.Name), hasParams, funcParams, funcParamDescriptions, d));
                        }
                    }
                }
            }
            foreach (FieldInfo info in targetType.GetFields(BindingFlags.Public | BindingFlags.Static)) {
                foreach (Attribute attr in Attribute.GetCustomAttributes(info)) {
                    if (attr.GetType() == typeof(ScriptVariableAttribute)) {
                        ScriptVariableAttribute scriptAttr = (ScriptVariableAttribute)attr;
                        string name = scriptAttr.GetName();
                        if (ScriptElements.ContainsKey(name)) {
                            ScriptElements.Remove(name);
                        }
                        // TODO: Currently broken in .net 4.0, needs security.
                        ScriptElements.Add(name, new ScriptElementDescriptor(name, scriptAttr.GetDescription(), PruneTypeName(info.FieldType.Name), info.GetValue(null)));
                    }
                }
            }
        }

        internal static void RegisterScriptElements(ScriptEngine engine) {
            foreach (KeyValuePair<string, ScriptElementDescriptor> d in ScriptElements) {
                if (d.Value.GetIsFunc()) {
                    engine.SetFunction(d.Key, d.Value.GetDelegate());
                } else {
                    engine.SetParameter(d.Key, d.Value.GetVarRef());
                }
            }
        }

        public static string Complete(string prefix) {
            IDictionaryEnumerator elements = ScriptElements.GetEnumerator();
            while (elements.MoveNext()) {
                ScriptElementDescriptor d = (ScriptElementDescriptor)elements.Value;
                string s = d.GetName();
                if (s.StartsWith(prefix)) {
                    if (s.Length > prefix.Length) {
                        return s.Substring(prefix.Length);
                    }
                }     
            }
            return Scripts.Complete(prefix);
        }

        [ScriptFunction("help", "Show help, or details for a given command or variable.\n Returns help string.",
            typeof(Jint.Delegates.Func<String, String>),
            "Optional command to get help for.")]
        public static string help(string command) {
            String r = "";
            if (String.IsNullOrWhiteSpace(command)) {
                String s;
                r = "Commands and variables:\n";
                IDictionaryEnumerator elements = ScriptElements.GetEnumerator();
                while (elements.MoveNext()) {
                    r += ((ScriptElementDescriptor)elements.Value).GetHeader() + "\n";
                }
                r += "Scripts:\n";
                s = Scripts.GetScriptNames(4);
                if (String.IsNullOrWhiteSpace(s)) s = "none\n";
                r += s;
                return r;
            } else if (ScriptElements.ContainsKey(command)) {
                r = ((ScriptElementDescriptor)ScriptElements[command]).GetFullDetails();
            } else if (Scripts.GetScriptFilePath(command) != null) {
                r = Scripts.GetScriptHelp(command);
            } else {
                r = "No such element: " + command;
            }
            return r;
        }

        [ScriptFunction("scripts", "List available script names.",
            typeof(Jint.Delegates.Func<String>))]
        public static string ListScripts() {
            String r = Scripts.GetScriptNames(1);
            if (String.IsNullOrWhiteSpace(r)) r = "none\n";
            return r;
        }
    }
}
