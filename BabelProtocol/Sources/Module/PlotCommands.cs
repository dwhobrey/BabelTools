using System;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Babel.Core;
using Babel.Resources;
using Babel.XLink;
using Jint;

using System.IO;
using System.Windows;
using System.Xml;

namespace Babel.BabelProtocol {

    public static class PlotCommands {

        [ScriptFunction("bplot", "Opens plot view. Returns plot Id.",
            typeof(Jint.Delegates.Func<String, int, bool, String>),
            "Exchange name.", "Message Id.", "Has time data.")]
        public static string BPlot(string exchangeName, int messageId = ProtocolConstants.IDENT_USER, bool hasTimeData = true) {
            if (!String.IsNullOrWhiteSpace(exchangeName)) {
                string id = PlotControl.OpenPlotShell();
                Shell s = Shell.GetShell(id);
                if (s != null) {
                    PlotControl p = s.MainControl as PlotControl;
                    if (p != null) {
                        BabelMessageDataCache d = new BabelMessageDataCache(exchangeName, messageId, true, hasTimeData);
                        p.SetCache(d);
                        return id;
                    }
                    s.Close();
                    return "Error: unable to open plot control.";
                }
                return "Error: unable to open plot shell.";
            }
            return "Error: bad exchange name.";
        }

        [ScriptFunction("bplotclose", "Closes plot and frees data cache.",
            typeof(Jint.Delegates.Func<String, String>),
            "Plot Id.")]
        public static string BPlotClose(string plotId) {
            if (!String.IsNullOrWhiteSpace(plotId)) {
                Shell s = Shell.GetShell(plotId);
                if (s != null) {
                    if (s.MainControl.Dispatcher.CheckAccess()) {
                        s.Close();
                        return "Closed plot:" + plotId;
                    } else {
                        return s.MainControl.Dispatcher.Invoke(new Func<string>(() => { return BPlotClose(plotId); }));
                    }
                }
                return "Error: unable to access plot shell.";
            }
            return "Error: plot Id.";
        }

        [ScriptFunction("bplotreset", "Resets plot data cache.",
            typeof(Jint.Delegates.Func<String,String>),
            "Plot Id.")]
        public static string BPlotReset(string plotId) {
            if (!String.IsNullOrWhiteSpace(plotId)) {
                Shell s = Shell.GetShell(plotId);
                if (s != null) {
                    if (s.MainControl.Dispatcher.CheckAccess()) {
                        PlotControl p = s.MainControl as PlotControl;
                        if (p != null) {
                            p.ResetCache();
                            return plotId;
                        }
                        return "Error: unable to open plot control.";
                    } else {
                        return s.MainControl.Dispatcher.Invoke(new Func<string>(() => { return BPlotReset(plotId); }));
                    }
                }
                return "Error: unable to open plot shell.";
            }
            return "Error: plot Id.";
        }

        [ScriptFunction("bplotlabels", "Sets plot titles.",
            typeof(Jint.Delegates.Func<String, String, String, String, String>),
            "Plot Id.", "Main title.", "Y axis title.", "X axis title.")]
        public static string BPlotTitles(string plotId, string title, string yAxis = "Y", string xAxis = "Time") {
            if (!String.IsNullOrWhiteSpace(plotId)) {
                Shell s = Shell.GetShell(plotId);
                if (s != null) {
                    if (s.MainControl.Dispatcher.CheckAccess()) {
                        PlotControl p = s.MainControl as PlotControl;
                        if (p != null) {
                            p.SetTitles(title, yAxis, xAxis);
                            return plotId;
                        }
                        return "Error: unable to open plot control.";
                    } else {
                        return s.MainControl.Dispatcher.Invoke(new Func<string>(() => { return BPlotTitles(plotId, title, yAxis, xAxis); }));
                    }
                }
                return "Error: unable to open plot shell.";
            }
            return "Error: plot Id.";
        }

        [ScriptFunction("bplotseriesname", "Sets plot series name.",
            typeof(Jint.Delegates.Func<String, int, String, String>),
            "Plot Id.", "Series Index.", "Series name.")]
        public static string BPlotSeriesName(string plotId, int seriesIndex, string seriesName) {
            if (!String.IsNullOrWhiteSpace(plotId)) {
                Shell s = Shell.GetShell(plotId);
                if (s != null) {
                    if (s.MainControl.Dispatcher.CheckAccess()) {
                        PlotControl p = s.MainControl as PlotControl;
                        if (p != null) {
                            p.SetSeriesName(seriesIndex, seriesName);
                            return plotId;
                        }
                        return "Error: unable to open plot control.";
                    }  else {
                        return s.MainControl.Dispatcher.Invoke(new Func<string>(() => { return BPlotSeriesName(plotId, seriesIndex, seriesName); }));
                    }
                }
                return "Error: unable to open plot shell.";
            }
            return "Error: plot Id.";
        }
    }
}
