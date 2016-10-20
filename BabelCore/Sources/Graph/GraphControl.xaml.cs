using System;
using System.IO;
using System.Windows;
using System.Xml;
using System.Windows.Controls;
using System.Drawing;
using Smrf.NodeXL.Core;
using Smrf.NodeXL.Visualization.Wpf;
using Babel.Resources;

namespace Babel.Core {

    public partial class GraphControl : ShellControl {

        static GraphControl() {
      //      DefaultStyleKeyProperty.OverrideMetadata(typeof(GraphControl),new FrameworkPropertyMetadata(typeof(GraphControl)));
        }

        public GraphControl() {
        }

        public GraphControl(string ownerShellId)
            : base(ownerShellId) {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        public GraphControl(string ownerShellId, XmlNode node)
            : base(ownerShellId) {
            InitializeComponent();
            Serializer(node, false);
            Loaded += OnLoaded;
        }

        public override void Init() {
            base.Init();         
        }

        public override void Close() {
            base.Close();
        }

        public override string KindName { get { return "graph"; } }

        // On entry, node is the shell node.
        public override void Serializer(XmlNode node, bool isSerialize) {
            base.Serializer(node, isSerialize);
            XmlNode c = Project.GetChildNode(node, "graph");
            if (c == null) return;
            if (isSerialize) {
                Project.SetNodeAttributeValue(c, "someproperty", "?");
            } else {
                string v = Project.GetNodeAttributeValue(c, "someproperty","?");
            }
        }

        void OnLoaded(object sender, RoutedEventArgs e) {
            PopulateAndDrawGraph();
        }

        // On entry, node is the shell node.
        [ShellDeserializer("graph")]
        public static ShellControl GraphShellDeserializer(string shellId, XmlNode node) {
            if (Application.Current.Dispatcher.CheckAccess()) {
                return new GraphControl(shellId, node);
            }
            return (ShellControl)Application.Current.Dispatcher.Invoke(
                    new Func<ShellControl>(() => { return GraphShellDeserializer(shellId, node); })
                );
        }

        [ScriptFunction("graph", "Opens graph view.",
            typeof(Jint.Delegates.Func<String>))]
        public static string OpenGraphShell() {
            if (Application.Current.Dispatcher.CheckAccess()) {
                Shell shell = Shell.OpenShell("Graph", 300, 500);
                string shellId = shell.ShellId;
                if (shell.MainControl == null) {
                    if (!shell.SetControl(new GraphControl(shellId))) {
                        return "Error: unable to create shell graph control.";
                    }
                }
                return shellId;
            }
            return (string)Application.Current.Dispatcher.Invoke(
                    new Func<string>(() => { return OpenGraphShell(); })
                );
        }

        protected void PopulateAndDrawGraph() {
            // Get the graph's vertex collection.

            IVertexCollection oVertices = Graph.Graph.Vertices;

            // Add three vertices.

            IVertex oVertexA = oVertices.Add();
            IVertex oVertexB = oVertices.Add();
            IVertex oVertexC = oVertices.Add();

            // Change the color, radius, and shape of vertex A.

            oVertexA.SetValue(ReservedMetadataKeys.PerColor,
                Color.FromArgb(255, 255, 0, 255));

            oVertexA.SetValue(ReservedMetadataKeys.PerVertexRadius, 20F);

            oVertexA.SetValue(ReservedMetadataKeys.PerVertexShape,
                VertexShape.Sphere);

            // Draw vertex B as a Label, which is a rectangle containing text.

            oVertexB.SetValue(ReservedMetadataKeys.PerVertexShape,
                VertexShape.Label);

            oVertexB.SetValue(ReservedMetadataKeys.PerVertexLabel, "Label");

            // Set the label's text and fill colors.

            oVertexB.SetValue(ReservedMetadataKeys.PerColor,
                Color.FromArgb(255, 220, 220, 220));

            oVertexB.SetValue(ReservedMetadataKeys.PerVertexLabelFillColor,
                Color.FromArgb(255, 0, 0, 0));

            // Annotate vertex C with text that is drawn outside the vertex.  All
            // shapes except Label can be annotated.

            oVertexC.SetValue(ReservedMetadataKeys.PerVertexLabel, "Annotation");

            // Get the graph's edge collection.

            IEdgeCollection oEdges = Graph.Graph.Edges;

            // Connect the vertices with directed edges.

            IEdge oEdge1 = oEdges.Add(oVertexA, oVertexB, true);
            IEdge oEdge2 = oEdges.Add(oVertexB, oVertexC, true);
            IEdge oEdge3 = oEdges.Add(oVertexC, oVertexA, true);

            // Customize their appearance.

            oEdge1.SetValue(ReservedMetadataKeys.PerColor,
                Color.FromArgb(255, 55, 125, 98));

            oEdge1.SetValue(ReservedMetadataKeys.PerEdgeWidth, 3F);
            oEdge1.SetValue(ReservedMetadataKeys.PerEdgeLabel, "This is edge 1");

            oEdge2.SetValue(ReservedMetadataKeys.PerEdgeWidth, 5F);
            oEdge2.SetValue(ReservedMetadataKeys.PerEdgeLabel, "This is edge 2");

            oEdge3.SetValue(ReservedMetadataKeys.PerColor,
                Color.FromArgb(255, 0, 255, 0));

            Graph.DrawGraph(true);
        }

    }
}

