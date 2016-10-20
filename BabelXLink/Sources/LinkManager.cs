using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Xml;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Babel.Core;
using Jint;

namespace Babel.XLink {

    /// <summary>
    /// The LinkManager maintains an active list of link devices.
    /// Devices are controlled by a particular module.
    /// It is up to the module to inform the manager of device events, 
    /// such as add and delete, read, write etc.
    /// Users can listen for device events by registering with the manager.
    /// </summary>
    public class LinkManager {

        public static LinkManager Manager = new LinkManager();

        /// <summary>
        /// The list of devices is keyed via a device's unique serial number + netIfIndex.
        /// </summary>
        ConcurrentDictionary<string, LinkDevice> Devices;
        
        /// <summary>
        /// A list of objects that are interested in manager events.
        /// </summary>
        ComponentListeners ManagerListeners;

        Object ManagerLock;

        public override string ToString() {
            string s = "devices:\n";
            lock (ManagerLock) {
                foreach (LinkDevice d in Devices.Values) {
                    s += d.ToString() + "\n";
                }
            }
            return s;
        }

        public List<string> GetDeviceIds() {
            List<string> a = new List<string>();
            lock (ManagerLock) {
                foreach (LinkDevice d in Devices.Values) {
                    a.Add(d.Id);
                }
            }
            return a;
        }

        private LinkManager() {
            Devices = new ConcurrentDictionary<string, LinkDevice>();
            ManagerListeners = new ComponentListeners();
            ManagerLock = new Object();
        }

        public void Clear() {
            lock (ManagerLock) {
                Devices.Clear();
                ManagerListeners.Clear();
                EventReporter.Clear();
            }
        }

        public void Close() {
            lock (ManagerLock) {
                foreach (LinkDevice d in Devices.Values) {
                    d.Close();
                }
            }
        }

        /// <summary>
        /// This listener is added to each device.
        /// It listens for all events and passes them on to all Manager Listeners.
        /// </summary>
        /// <param name="ev">The event.</param>
        /// <param name="component">The device the event occurred on.</param>
        /// <param name="val">The object associated with the event, such as a byte buffer or device state.</param>
        public void ComponentEventListener(ComponentEvent ev, Component component, object val) {
            ManagerListeners.NotifyListeners(ev, component, val);
        }

        /// <summary>
        /// Add a delegate as a listener for device events posted to the manager.
        /// </summary>
        /// <param name="owner">The owner object, such as a Shell.</param>
        /// <param name="d">The delegate to notify about the event.</param>
        public void AddListener(Object owner, ComponentListenerDelegate d) {
            ManagerListeners.AddListener(owner, d);
        }

        public void RemoveListener(Object owner, ComponentListenerDelegate d) {
            ManagerListeners.RemoveListener(owner, d);
        }

        // Fetch a list of the devices matching pattern.
        public List<LinkDevice> GetDevices(string deviceIdPattern) {
            List<LinkDevice> devs = new List<LinkDevice>();
            lock (ManagerLock) {
                foreach (LinkDevice d in Devices.Values) {
                    if (d.Id != null && (String.IsNullOrWhiteSpace(deviceIdPattern)
                            || Regex.IsMatch(d.Id, deviceIdPattern, RegexOptions.IgnoreCase))
                        ) {
                        devs.Add(d);
                    }
                }
            }
            return devs;
        }

        /// <summary>
        /// Find device in devices list.
        /// </summary>
        /// <param name="instanceId">The string for the device's unique serial number + netIfIndex.</param>
        /// <returns>Returns LinkDevice object for the device or null if not found.</returns>
        public LinkDevice FindDevice(string instanceId) {
            LinkDevice d = null;
            lock (Manager) {
                if (String.IsNullOrWhiteSpace(instanceId) || !Devices.TryGetValue(instanceId, out d)) d = null;
            }
            return d;
        }

        /// <summary>
        /// Remove device from devices list.
        /// </summary>
        /// <param name="instanceId">The string for the device's unique serial number + netIfIndex.</param>
        public void DeleteDevice(string instanceId) {
            lock (Manager) {
                if (!String.IsNullOrWhiteSpace(instanceId) && Devices.ContainsKey(instanceId)) {
                    LinkDevice d = null;
                    if (Devices.TryRemove(instanceId, out d)) {
                        ManagerListeners.NotifyListeners(ComponentEvent.ComponentRemove, d, null);
                    }
                }
            }
        }

        /// <summary>
        /// Add device to devices list.
        /// Also adds the manager to devices event listener list.
        /// </summary>
        /// <param name="instanceId">The string for the device's unique serial number + netIfIndex.</param>
        /// <param name="d">The device to add.</param>
        /// <returns>Returns false if not added, e.g. because already in list.</returns>
        public bool AddDevice(string instanceId, LinkDevice d) {
            bool result = false;
            lock (Manager) {
                if (String.IsNullOrWhiteSpace(instanceId)) return false;
                result = Devices.TryAdd(instanceId, d);
            }
            if (result) {
                ManagerListeners.NotifyListeners(ComponentEvent.ComponentAdd, d, null);
                d.AddListener(Manager, ComponentEventListener); // Add the manager as a listener.
            }
            return result;
        }

        public static void ConfigProject() {
            Manager.Clear();
            EventReporter.ConfigProject();
        }

        public static void StartProject() {       
            EventReporter.StartProject();
        }
        public static void CloseProject() {
            Manager.Close();
            EventReporter.CloseProject();
        }

        public static void EndApp() {
        }

        [ScriptFunction("dev", "Returns the list of devices.",typeof(Jint.Delegates.Func<String>))]
        public static string GetDevices() {
            return Manager.ToString();
        }
    }
}
