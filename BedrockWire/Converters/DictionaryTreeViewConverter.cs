using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using Avalonia.Media;
using BedrockWire.Core.Model;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace BedrockWire.Converters
{
    public class DictionaryTreeViewConverter : IValueConverter
    {
        private TreeViewItem ObjectToTreeViewItem(string name, object obj)
        {
            TreeViewItem item = new TreeViewItem();
            item.FontSize = 14;
            item.MinHeight = 24;
       

            if (obj is Dictionary<object, object> dict)
            {
                var items = new List<TreeViewItem>();
                foreach (var key in dict.Keys)
                {
                    var value = dict[key];
                    items.Add(ObjectToTreeViewItem(key.ToString(), value));
                }
                item.Items = items;
                item.Header = name;
            }
            else if(obj != null)
            {
                string text = obj.ToString();
                string original = text;

                if(text.Length > 40)
                {
                    text = "...";
                }
                
                
                var box = new TextBlock();
                box.Text = name + ": " + text;
                box.DoubleTapped += new EventHandler<RoutedEventArgs>(delegate (object obj, RoutedEventArgs args)
                {
                    Application.Current.Clipboard.SetTextAsync(original);
                });
                item.Header = box;

            }
            else
            {
                item.Header = "null";
            }

            return item;
        }

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if(value is Packet p && p.Decoded != null)
            {
                return new List<TreeViewItem>() { ObjectToTreeViewItem("Root", p.Decoded) };
            }
            return null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
