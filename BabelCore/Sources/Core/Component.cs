using System;

namespace Babel.Core {

    public class ComponentState {

        public class Ids {
		    public const int NotConfigured = 0;
		    public const int Working = 1;
            public const int Unresponsive = 2;
		    public const int Problem = 3;
		    public const int Closing = 4;
		    public const int Suspended = 5;
		    public const int NotConnected = 6;
            public const int NotPermitted = 7;
	    }

        public static readonly ComponentState NotConfigured = new ComponentState("NotConfigured", Ids.NotConfigured);
        public static readonly ComponentState Working = new ComponentState("Working", Ids.Working);
        public static readonly ComponentState Unresponsive = new ComponentState("Unresponsive", Ids.Unresponsive);
        public static readonly ComponentState Problem = new ComponentState("Problem", Ids.Problem);
        public static readonly ComponentState Closing = new ComponentState("Closing", Ids.Closing);
        public static readonly ComponentState Suspended = new ComponentState("Suspended", Ids.Suspended);
        public static readonly ComponentState NotConnected = new ComponentState("NotConnected", Ids.NotConnected);
        public static readonly ComponentState NotPermitted = new ComponentState("NotPermitted", Ids.NotPermitted);

        public string Name;
        public int Id;

        private static int IdCounter = 0;

        public ComponentState(string name, int id = -1) {
            Name = name;
            if (id < 0) Id = IdCounter++;
            else Id = id;
            if (IdCounter < Id) IdCounter = 1 + Id;
        }

        public bool Equals(ComponentState ev) {
            if (ev == null) return false;
            return this.Id == ev.Id;
        }
    }

    /// <summary>
    /// Base class for main components on which events can be raised.
    /// </summary>
    public class Component {

        private static int SessionCounter = 0;

        public string Id;
        public ComponentState State;
        public int SessionId;
        protected ComponentListeners ComponentListeners;

        public Component() {
            Id = null;
            State = ComponentState.NotConfigured;
            SessionId = ++SessionCounter;
            ComponentListeners = new ComponentListeners();
        }

        public string GetComponentId() {
            return Id;
        }
        public ComponentState GetComponentState() {
            return State;
        }
        public int GetSessionId() {
            return SessionId;
        }
        public void SetSessionId() {
            SessionId = ++SessionCounter;
        }
        public void AddListener(Object owner, ComponentListenerDelegate d) {
            ComponentListeners.AddListener(owner, d);
        }

        protected void NotifyListeners(ComponentEvent ev, object val) {
            ComponentListeners.NotifyListeners(ev, this, val);
        }
        // This should interrupt i/o threads if changing from Working state.
        public void NotifyStateChange(ComponentState s) {
            if (s != State) {
                State = s;
                NotifyListeners(ComponentEvent.StateChange, s);
            }
        }
    }
}
