using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Babel.Core;

namespace Babel.Core {

    public class ComponentEvent {

        public static readonly ComponentEvent None = new ComponentEvent("None",0);
        public static readonly ComponentEvent StateChange = new ComponentEvent("StateChange", 1);
        public static readonly ComponentEvent ComponentAdd = new ComponentEvent("ComponentAdd", 2);
        public static readonly ComponentEvent ComponentRemove = new ComponentEvent("ComponentRemove", 3);
        public static readonly ComponentEvent ComponentSuspend = new ComponentEvent("ComponentSuspend", 4);
        public static readonly ComponentEvent ComponentResume = new ComponentEvent("ComponentResume", 5);
        public static readonly ComponentEvent ReadComplete = new ComponentEvent("ReadComplete", 6);
        public static readonly ComponentEvent WriteComplete = new ComponentEvent("WriteComplete", 7);

        public string Name;
        public int Id;

        private static int IdCounter = 0;

        public ComponentEvent(string name, int id=-1) {
            Name = name;
            if (id < 0) Id = IdCounter++;
            else Id = id;
            if (IdCounter < Id) IdCounter = 1+Id;
        }

        public bool Equals(ComponentEvent ev) {
            if (ev == null) return false;
            return this.Id==ev.Id;
        }
    }

    /// <summary>
    /// A delegate for component event notifications.
    /// </summary>
    /// <param name="e">The event.</param>
    /// <param name="c">The component on which the event occurred.</param>
    /// <param name="v">The object associated with the event, such as a byte array or device state.</param>
    public delegate void ComponentListenerDelegate(ComponentEvent ev, Component component, object val);

    /// <summary>
    /// Maintains a list of Component listeners keyed by Owner objects.
    /// </summary>
    public class ComponentListeners {

        ConcurrentDictionary<Object, List<ComponentListenerDelegate>> Owners;

        public ComponentListeners() {
            Owners = new ConcurrentDictionary<Object, List<ComponentListenerDelegate>>();
        }

        public void AddListener(Object owner, ComponentListenerDelegate d) {
            lock (Owners) {
                List<ComponentListenerDelegate> group = null;
                if(owner!=null) 
                    Owners.TryGetValue(owner, out group);
                if (group == null) {
                    group = new List<ComponentListenerDelegate>();
                    Owners.TryAdd(owner, group);
                }
                group.Add(d);
            }
        }

        public void RemoveListener(Object owner, ComponentListenerDelegate d) {
            lock (Owners) {
                List<ComponentListenerDelegate> group = null;
                if (owner != null) {
                    Owners.TryGetValue(owner, out group);
                    if (group != null) {                  
                        group.Remove(d);
                        if (group.Count == 0) {
                            Owners.TryRemove(owner, out group);
                        }
                    }
                }
            }
        }

        public void NotifyListeners(ComponentEvent ev, Component component, object val) {
            lock (Owners) {
                foreach (List<ComponentListenerDelegate> group in Owners.Values) {
                    foreach (ComponentListenerDelegate d in group) {
                        d(ev, component, val);
                    }
                }
            }
        }

        public void Clear() {
            Owners.Clear();
        }
    }
}
