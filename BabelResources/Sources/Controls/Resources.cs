using System;
using System.Collections.Generic;
using System.Windows;

namespace Babel.Resources {
    public static class Resource {

        private static readonly Dictionary<Uri, ResourceDictionary> SharedDictionaries = new Dictionary<Uri, ResourceDictionary>();

        private static void onMergedDictionaryChanged(DependencyObject source, DependencyPropertyChangedEventArgs e) {
            Object v = (e == null) ? null : e.NewValue;
            string s = (v == null) ? null : v.ToString();
            if (String.IsNullOrWhiteSpace(s)) {
                //throw new System.ArgumentException("onMergedDictionaryChanged: value must be a resource relative uri string.", "e");
                return;
            }
          //  MessageBox.Show("S="+s+".");
            Uri resourceLocator = new Uri(s, UriKind.Relative);
            if (resourceLocator!=null && resourceLocator.IsAbsoluteUri && resourceLocator.Scheme.Equals("pack")) {
                resourceLocator = new Uri(resourceLocator.AbsolutePath, UriKind.Relative);
            }
            if (resourceLocator == null) {
                throw new System.ArgumentException("onMergedDictionaryChanged: null resource locator.", s);
            }
            ResourceDictionary dictionary = null;
            if (SharedDictionaries.ContainsKey(resourceLocator))
                dictionary = SharedDictionaries[resourceLocator];
            else {
                dictionary = (ResourceDictionary)Application.LoadComponent(resourceLocator);
                if (dictionary == null) {
                    throw new System.ArgumentException("onMergedDictionaryChanged: unable to load dictionary.", s);
                }
                SharedDictionaries.Add(resourceLocator, dictionary);
            }
            if (source is FrameworkElement) {
                ((FrameworkElement)source).Resources.MergedDictionaries.Add(dictionary);
            } else if (source is FrameworkContentElement) {
                ((FrameworkContentElement)source).Resources.MergedDictionaries.Add(dictionary);
            } else {
                throw new System.ArgumentException("onMergedDictionaryChanged: source must be a FrameworkElement.", "source");
            }
        }

        public static readonly DependencyProperty MergedDictionaryProperty =
            DependencyProperty.RegisterAttached("MergedDictionary", typeof(String), typeof(Resource), 
            new FrameworkPropertyMetadata(null, new PropertyChangedCallback(onMergedDictionaryChanged)));

        [AttachedPropertyBrowsableForType(typeof(FrameworkElement))]
        public static String GetMergedDictionary(DependencyObject source) {
            if (source == null) return null;
            return (String)source.GetValue(MergedDictionaryProperty);
        }

        public static void SetMergedDictionary(DependencyObject source, String value) {
            if (source==null) {
                throw new System.ArgumentException("SetMergedDictionary: arg null.", "source");
            }
            if (String.IsNullOrWhiteSpace(value)) {
                throw new System.ArgumentException("SetMergedDictionary: arg null.", "value");
            }
            source.SetValue(MergedDictionaryProperty, value);
        }
    }
}
