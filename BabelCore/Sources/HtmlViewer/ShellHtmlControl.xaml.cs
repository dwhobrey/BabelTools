using System;
using System.Collections;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Threading;
using System.Windows.Threading;
using System.Xml;
using Babel.Resources;

namespace Babel.Core {

    public partial class ShellHtmlControl : ScriptControl {

        private readonly static string DefaultShellTemplateFile = "shell.html";
        private readonly static string DefaultShellCssFile = "shell.css";

        private string ShellTemplateFileName, ShellCssFileName;
        private string ShellTemplate, ShellCss, HtmlText;
        private HTMLConverter Converter;

        static ShellHtmlControl() {
  //          DefaultStyleKeyProperty.OverrideMetadata(typeof(ShellHtmlControl),new FrameworkPropertyMetadata(typeof(ShellHtmlControl)));
        }

        public ShellHtmlControl() {
        }

        private ShellHtmlControl(string ownerShellId, string startScriptName, string endScriptName) 
            : base(ownerShellId,startScriptName, endScriptName, false) {
            ShellTemplateFileName = DefaultShellTemplateFile;
            ShellCssFileName = DefaultShellCssFile;
            ShellTemplate = null;
            ShellCss = null;
            HtmlText = null;
            Converter = null;
            InitializeComponent();
        }

        private ShellHtmlControl(string ownerShellId,XmlNode node)
            : base(ownerShellId) {
            ShellTemplateFileName = DefaultShellTemplateFile;
            ShellCssFileName = DefaultShellCssFile;
            ShellTemplate = null;
            ShellCss = null;
            HtmlText = null;
            Converter = null;
            InitializeComponent();
            Serializer(node,false);
        }

        public override string KindName { get { return "html"; } }

        // On entry, node is the shell node.
        public override void Serializer(XmlNode node, bool isSerialize) {
            base.Serializer(node, isSerialize);
            XmlNode c = Project.GetChildNode(node, "html");
            if (c == null) return;
            if (isSerialize) {
                Project.SetNodeAttributeValue(c, "shelltemplate", ShellTemplateFileName);
                Project.SetNodeAttributeValue(c, "shellcss", ShellCssFileName);
            } else {
                ShellTemplateFileName = Project.GetNodeAttributeValue(c, "shelltemplate", DefaultShellTemplateFile);
                ShellCssFileName = Project.GetNodeAttributeValue(c, "shellcss", DefaultShellCssFile);
            }
        }

        public override void Close() {
            base.Close();
            if (Converter != null) {
                Converter.Close();
                Converter = null;
            }
        }

        private void MergeHTML(string text) {
            string f;
            HtmlText = text;
            if (text != null) {
                if (String.IsNullOrWhiteSpace(ShellTemplate)) {
                    f = Shell.FindFile(OwnerShellId,ShellTemplateFileName);
                    if (f == null) return;
                    ShellTemplate = Settings.ReadInFile(f);
                }
                if (String.IsNullOrWhiteSpace(ShellCss)) {
                    f = Shell.FindFile(OwnerShellId,ShellCssFileName);
                    if (f == null) return;
                    ShellCss = Settings.ReadInFile(f);
                }
                ArrayList args = new ArrayList();
                args.Add(ShellCss);
                args.Add(text);
                HtmlText = Scripts.SubVars(ShellTemplate, args);
            }
        }

        private void UpdateHTML() {
            if (HtmlText != null) {
                if (Converter == null) Converter = new HTMLConverter(this);
                Contents.Source = Converter.ConvertToBitmapImage(HtmlText);
            }
        }

        public override void ShellSizeChanged() {
            UpdateHTML();
        }

        public override void Clear() {
            base.Clear();
            HtmlText = null;
            UpdateHTML();
        }

        public override void Write(string text, bool onPromptLine = false, bool onNewLine = false) {
            if (text == null) Converter = null;
            MergeHTML(text);
            UpdateHTML();
        }

        // On entry, node is the shell node.
        [ShellDeserializer("html")]
        public static ShellControl HtmlShellDeserializer(string shellId, XmlNode node) {
            if (Application.Current.Dispatcher.CheckAccess()) {
                return new ShellHtmlControl(shellId, node);
            }
            return (ShellControl)Application.Current.Dispatcher.Invoke(
                    new Func<ShellControl>(() => { return HtmlShellDeserializer(shellId,node); })
                );
        }

        [ScriptFunction("html", "Opens a new html shell. Returns shell Id.",
           typeof(Func<String, int, int, int, int, int, String, String, String, String, String>),
           "Title for shell.",
           "Width.", "Height.", "X position.", "Y position.", "Window state {0,1,2}.",
            "Shell Id.", "Working dir.",
           "Start up script: 'shellstart'.", "End script: 'shellend'."
           )]
        public static string OpenHtmlShell(string title,
            int width = 300, int height = 200, int x = -1, int y = -1, int windowState=0,
            string shellId = "", string workingDir = "", 
            string scriptStart = "shellstart", string scriptEnd = "shellend") {
            if (Application.Current.Dispatcher.CheckAccess()) {
                Shell shell = Shell.OpenShell(title, width, height, x, y, windowState, shellId, workingDir);
                shellId = shell.ShellId;
                if (shell.MainControl == null) {
                    if (!shell.SetControl(new ShellHtmlControl(shellId,scriptStart,scriptEnd))) {
                        return "Error: unable to create shell script control.";
                    }
                }
                return shellId;
            }
            return (string)Application.Current.Dispatcher.Invoke(
                    new Func<string>(() => { 
                        return OpenHtmlShell(title, width, height, x, y, windowState, shellId, workingDir, scriptStart, scriptEnd); })
                );
        }

        [ScriptFunction("htmltemplate", "Get or set shell html template.",
            typeof(Jint.Delegates.Func<String, String>),
            "Id of shell.", "File name of template.")]
        public static string HtmlTemplate(string id, string fileName="") {
            if (!String.IsNullOrWhiteSpace(id)) {
                Shell s = Shell.GetShell(id);
                if (s != null) {
                    if (s.MainControl.Dispatcher.CheckAccess()) {
                        ShellHtmlControl h = s.MainControl as ShellHtmlControl;
                        if (h==null) return "Error: no html control.";
                        if(!String.IsNullOrWhiteSpace(fileName)) 
                            h.ShellTemplateFileName = fileName;
                        return h.ShellTemplateFileName;
                    } else {
                        return (string)s.MainControl.Dispatcher.Invoke(new Func<string>(()
                            => { return HtmlTemplate(s.ShellId, fileName); }));
                    }
                }
            }
            return "";
        }

        [ScriptFunction("htmlcss", "Get or set shell html style sheet.",
            typeof(Jint.Delegates.Func<String, String>),
            "Id of shell.", "File name of style sheet.")]
        public static string HtmlCss(string id, string fileName = "") {
            if (!String.IsNullOrWhiteSpace(id)) {
                Shell s = Shell.GetShell(id);
                if (s != null) {
                    if (s.MainControl.Dispatcher.CheckAccess()) {
                        ShellHtmlControl h = s.MainControl as ShellHtmlControl;
                        if (h == null) return "Error: no html control.";
                        if (!String.IsNullOrWhiteSpace(fileName))
                            h.ShellCssFileName = fileName;
                        return h.ShellCssFileName;
                    } else {
                        return (string)s.MainControl.Dispatcher.Invoke(new Func<string>(()
                            => { return HtmlCss(s.ShellId, fileName); }));
                    }
                }
            }
            return "";
        }
    }
}
