using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using BedrockWire.Models;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace BedrockWire.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // autoscroll for datagrid
            bool isAtEnd = true;

            datagrid.VerticalScroll += (sender, args) =>
            {
                var scrollBar = sender as ScrollBar;

                isAtEnd = false;

                if (args.ScrollEventType != ScrollEventType.SmallDecrement && args.ScrollEventType != ScrollEventType.LargeDecrement)
                {
                    isAtEnd = scrollBar.Value == scrollBar.Maximum;
                }
            };

            datagrid.GetObservable(DataGrid.ItemsProperty).Subscribe(x =>
            {
                if (x is DataGridCollectionView collection)
                {
                    collection.CollectionChanged += (sender, args) =>
                    {
                        if(args.Action == NotifyCollectionChangedAction.Add && isAtEnd)
                        {
                            datagrid.ScrollIntoView(collection[collection.Count - 1], null);
                        }
                    };
                }
            });
        }
    }
}
