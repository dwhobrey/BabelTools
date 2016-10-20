using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Babel.Resources {
    public class FileSystemNodeImageConverter : IValueConverter {

        public ImageSource DriveImage { get; set; }
        public ImageSource DirectoryImage { get; set; }
        public ImageSource BlackFileImage { get; set; }
        public ImageSource BlueFileImage { get; set; }
        public ImageSource GreenFileImage { get; set; }
        public ImageSource GreyFileImage { get; set; }
        public ImageSource PurpleFileImage { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            TreeViewItem node = value as TreeViewItem;
            if (node!=null) {
                if (node.Tag is DriveInfo) {
                    return DriveImage;
                }
                if (node.Tag is DirectoryInfo) {
                    return DirectoryImage;
                }
                if (node.Tag is FileInfo) {
                    FileInfo f = (FileInfo)node.Tag;
                    switch (f.Extension) {
                        case ".bs": return GreenFileImage;
                        case ".bsx": return BlueFileImage;
                        case ".bsd": return PurpleFileImage;
                    }
                    return GreyFileImage;
                }
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}
