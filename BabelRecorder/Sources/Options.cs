using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Input;
using System.Xml;
using System.Windows.Controls;

using Babel.Resources;
using Babel.Core;
using Babel.XLink;
using Babel.BabelProtocol;

using Xceed.Wpf.Toolkit;

namespace Babel.Recorder {

    public class Options {

        RecorderControl Recorder;

        public Options(RecorderControl recorder) {
            Recorder = recorder;
        }

        public void InitializeControls() {
          
        }

        public void PostInit() {
        }

        public void SerializeOptions(XmlNode node, bool isSerialize) {
            XmlNode options = Project.GetChildNode(node, "options");
            if (options == null) return;
            XmlNode chart = Project.GetChildNode(options, "chart");
            if (chart != null) {
            }
            XmlNode general = Project.GetChildNode(options, "general");
            if (general != null) {
                TextBox tb = Recorder.FindName("LockPassword") as TextBox;
                if (isSerialize) {
                    Project.SetNodeAttributeValue(general, "lockpassword", (tb == null) ? "" : tb.Text);
                } else {
                    if (tb != null) tb.Text = Project.GetNodeAttributeValue(general, "lockpassword", ""); ;
                }
            }
        }
    }
}
