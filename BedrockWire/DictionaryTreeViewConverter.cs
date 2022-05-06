using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BedrockWire
{
    public class DictionaryTreeViewConverter : IValueConverter
    {
        private TreeViewItem ObjectToTreeViewItem(string name, object obj)
        {
            TreeViewItem item = new TreeViewItem();

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
            else
            {
                string text = obj.ToString();
                string original = text;

                if(text.Length > 40)
                {
                    text = text.Substring(0, 40) + "...";
                }
                
                
                var box = new TextBlock();
                box.Text = name + ": " + text;
                box.DoubleTapped += new EventHandler<RoutedEventArgs>(delegate (object obj, RoutedEventArgs args)
                {
                    Application.Current.Clipboard.SetTextAsync(original);
                });
                item.Header = box;

            }

            return item;
        }

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return new List<TreeViewItem>() { ObjectToTreeViewItem("Root", value) };
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
