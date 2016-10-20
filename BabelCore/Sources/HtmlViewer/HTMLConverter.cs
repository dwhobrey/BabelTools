using System;
using System.IO;
using System.Text;
using System.ComponentModel;
using System.Threading;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Media.Imaging;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace Babel.Core {
    public partial class HTMLConverter  {

        WebBrowser WB;
        System.Windows.Controls.Control Ctrl;

        public HTMLConverter(System.Windows.Controls.Control control) {
            WB = new WebBrowser();
            Ctrl = control;
            WB.Size = new Size(400, 200);
            WB.Dock = DockStyle.Fill;
            WB.ScrollBarsEnabled = false;
          //  wb.AllowNavigation = false; // Won't update if set to false.
            WB.AllowWebBrowserDrop = false;
            WB.Navigate("about:blank");
            WB.DocumentText = ""; // First time text won't be show, so flush via empty text.
            WB.PerformLayout();
            Application.DoEvents();
            while (WB.ReadyState != WebBrowserReadyState.Complete) Thread.Sleep(100);
        }

        public void Close() {
        }

        // Replacement for mshtml imported interface.
        [ComImport, InterfaceType((short)1), Guid("3050F669-98B5-11CF-BB82-00AA00BDCE0B")]
        interface IHTMLElementRender {
            void DrawToDC(IntPtr hdc);
            void SetDocumentPrinter(string bstrPrinterName, IntPtr hdc);
        }

        public BitmapImage ConvertToBitmapImage(string htmlText) {
            BitmapImage bitmapImage = null;
            WB.Width = (int)Ctrl.ActualWidth;
            WB.Height = (int)Ctrl.ActualHeight;
            WB.DocumentText = htmlText;
            Application.DoEvents();
            while (WB.ReadyState!=WebBrowserReadyState.Complete) Thread.Sleep(100);
            // Get the renderer for the document body
            mshtml.IHTMLDocument2 doc = (mshtml.IHTMLDocument2)WB.Document.DomDocument;
            mshtml.IHTMLElement body = (mshtml.IHTMLElement)doc.body;
            IHTMLElementRender render = (IHTMLElementRender)body;
            // Render to bitmap
            using (Bitmap bmp = new Bitmap(WB.Width, WB.Height)) {
                using (Graphics gr = Graphics.FromImage(bmp)) {
                    IntPtr hdc = gr.GetHdc();
                    render.DrawToDC(hdc);
                    gr.ReleaseHdc();
                }
                MemoryStream memory = new MemoryStream();
                bmp.Save(memory, ImageFormat.Png);
                memory.Position = 0;
                bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.StreamSource = memory;
                bitmapImage.EndInit();
                memory.Dispose();
                memory = null;
            }
            return bitmapImage;
        }

#if false // Test code for alternate HTMLRenderer.
        public void ConvertViaHTMLRenderer() {
            Bitmap bitmap = new Bitmap((Int32)ActualWidth, (Int32)ActualHeight, PixelFormat.Format32bppArgb);
            CssData d = CssData.Parse("body {font-family:'Times New Roman';font-size: 12pt}");
            Graphics g = Graphics.FromImage(bitmap);
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            HtmlRender.Render(g, s, new PointF(), new SizeF(), d);
        }
#endif
    }
}
