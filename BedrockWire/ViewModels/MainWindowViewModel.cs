using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using BedrockWire.Models;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml;

namespace BedrockWire.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        public List<Packet> PacketList { get; set; }
        public ObservableCollection<Packet> FilteredPacketList { get; set; }
        private Dictionary<int, PacketDefinition> PacketDefinitions { get; set; }

        private bool filterOutMoveDelta;
        public bool FilterOutMoveDelta
        {
            get => filterOutMoveDelta;
            set {
                filterOutMoveDelta = value;
                RefreshFilters();
            }
        }

        private string filterText;
        public string FilterText
        {
            get => filterText;
            set
            {
                filterText = value;
                RefreshFilters();
            }
        }

        private Packet selectedPacket;
        public Packet SelectedPacket
        {
            get => selectedPacket;
            set => this.RaiseAndSetIfChanged(ref selectedPacket, value);
        }

        public string StatusText { get; set; }

        public MainWindowViewModel()
        {
            PacketList = new List<Packet>();
            FilteredPacketList = new ObservableCollection<Packet>();
            PacketDefinitions = new Dictionary<int, PacketDefinition>();
            UpdateStatusText();
        }

        private void DecodePacket(Packet packet)
        {
            packet.Name = "UNKNOWN_PACKET";
            if (PacketDefinitions != null && PacketDefinitions.ContainsKey(packet.Id))
            {
                packet.Name = PacketDefinitions[packet.Id].Name;

                try
                {
                    using (MemoryStream stream = new MemoryStream(packet.Payload))
                    {
                        using (BinaryReader reader2 = new BinaryReader(stream))
                        {
                            packet.Decoded = PacketDecoder.Decode(reader2, PacketDefinitions[packet.Id].Fields);
                            if (stream.Position != stream.Length)
                            {
                                packet.Error = (stream.Length - stream.Position) + " bytes left unread";
                            }
                        }
                    }

                }
                catch (Exception ex)
                {
                    packet.Error = ex.Message;
                }
            }
            else
            {
                packet.Error = "Unknown packet id: " + packet.Id;
            }
        }

        private void UpdateStatusText()
        {
            StatusText = "Loaded " + PacketDefinitions.Count + " packet definitions. Showing " + FilteredPacketList.Count + "/" + PacketList.Count + " packets. " + PacketList.Where(p => p.Error != null).Count() + "/" + PacketList.Count + " have errors.";
            this.RaisePropertyChanged(nameof(StatusText));
        }

        public async void OnOpenCommand(Window window)
        {
            var dlg = new OpenFileDialog();
            dlg.Filters.Add(new FileDialogFilter() { Name = "BedrockWire Files", Extensions = { "bw" } });
            dlg.AllowMultiple = false;

            var result = await dlg.ShowAsync(window);
            if (result != null && result.Length > 0)
            {
                PacketList = new List<Packet>();
                this.RaisePropertyChanged(nameof(PacketList));

                var stream = new FileStream(result[0], FileMode.Open);

                byte[] header = new byte[4];
                stream.Read(header, 0, 4);

                if (header[0] != 66 || header[1] != 68 || header[2] != 87)
                {
                    return;
                }

                byte version = header[3];

                if (version != 1)
                {
                    return;
                }

                BinaryReader reader = new BinaryReader(new DeflateStream(stream, CompressionMode.Decompress));
                new Thread(() =>
                {
                    try
                    {
                        while (true)
                        {
                            byte direction = reader.ReadByte();
                            byte packetId = reader.ReadByte();
                            ulong time = reader.ReadUInt64();
                            int length = reader.ReadInt32();
                            byte[] payload = reader.ReadBytes(length);

                            string name = "UNKNOWN_PACKET";

                            var packet = new Packet() { Direction = direction == 0 ? "C -> S" : "S -> C", Id = packetId, Payload = payload, Time = time, Length = (ulong)length };
                            DecodePacket(packet);

                            PacketList.Add(packet);
                            if (IsPacketVisible(packet))
                            {
                                Dispatcher.UIThread.InvokeAsync(() =>
                                {
                                    FilteredPacketList.Add(packet);
                                    UpdateStatusText();
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                    }

                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        RefreshFilters();
                    });
                    reader.Close();
                }).Start();
            }
        }

        private bool IsPacketVisible(Packet packet)
        {
            if (FilterOutMoveDelta && packet.Id == 111)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(FilterText))
            {
                if (FilterText.StartsWith("0x") && FilterText.Length > 2)
                {
                    try
                    {
                        int id = Convert.ToInt32(FilterText, 16);
                        if (packet.Id != id)
                        {
                            return false;
                        }
                    }
                    catch (Exception)
                    {
                    }

                }
                else if (!packet.Name.Contains(FilterText, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
            return true;

        }

        private void RefreshFilters()
        {
            FilteredPacketList.Clear();

            foreach(Packet packet in PacketList)
            {
                if(IsPacketVisible(packet))
                {
                    FilteredPacketList.Add(packet);
                }
            }

            UpdateStatusText();
        }

        private PacketField ParseField(XmlNode node)
        {
            PacketField packetField = new PacketField() {
                Name = node.Attributes.GetNamedItem("name")?.InnerText,
                Type = node.Attributes.GetNamedItem("type")?.InnerText,
                IsSwitch = node.Name == "switch",
                IsList = node.Name == "list",
                IsFlags = node.Name == "flags",
                Case = node.Name == "case" ? node.Attributes.GetNamedItem("value")?.InnerText : null,
                Conditional = node.Name == "conditional" ? node.Attributes.GetNamedItem("condition")?.InnerText : null,
                ReferenceId = node.Attributes.GetNamedItem("refId")?.InnerText,
                ReferencesId = node.Attributes.GetNamedItem("ref")?.InnerText,
                SubFields = new List<PacketField>(),
            };

            foreach (XmlNode fieldNode in node.ChildNodes)
            {
                if(fieldNode.NodeType == XmlNodeType.Element)
                {
                    packetField.SubFields.Add(ParseField(fieldNode));
                }
                
            }

            return packetField;
        }

        public async void DecodeCommand(Packet packet)
        {
            DecodePacket(packet);
        }

        public async void OnLoadProtocolCommand(Window window)
        {
            var dlg = new OpenFileDialog();
            dlg.Filters.Add(new FileDialogFilter() { Name = "Bedrock Protocol Definition", Extensions = { "xml" } });
            dlg.AllowMultiple = false;

            var result = await dlg.ShowAsync(window);
            if (result != null && result.Length > 0)
            {
                PacketDefinitions = new Dictionary<int, PacketDefinition>();

                XmlDocument doc = new XmlDocument();
                doc.Load(result[0]);

                FieldDecoders.CustomDecoders.Clear();

                foreach (XmlNode node in doc.DocumentElement.SelectNodes("child::types")[0])
                {
                    if (node.Name == "type")
                    {
                        string name = node.Attributes.GetNamedItem("name").InnerText;

                        List<PacketField> fields = new List<PacketField>();

                        foreach (XmlNode fieldNode in node.ChildNodes)
                        {
                            if (fieldNode.NodeType == XmlNodeType.Element)
                            {
                                fields.Add(ParseField(fieldNode));
                            }
                        }

                        FieldDecoders.CustomDecoders.Add(name, fields);
                    }
                }

                foreach (XmlNode node in doc.DocumentElement.SelectNodes("child::packets")[0])
                {
                    if(node.Name == "packet")
                    {
                        int id = Convert.ToInt32(node.Attributes.GetNamedItem("id").InnerText, 16);
                        string name = node.Attributes.GetNamedItem("name").InnerText;

                        List<PacketField> fields = new List<PacketField>();

                        foreach(XmlNode fieldNode in node.ChildNodes)
                        {
                            if (fieldNode.NodeType == XmlNodeType.Element)
                            {
                                fields.Add(ParseField(fieldNode));
                            }
                        }

                        PacketDefinitions[id] = new PacketDefinition() { Name = name, Fields = fields };
                    }
                }

                foreach(Packet packet in PacketList)
                {
                    DecodePacket(packet);
                }

                RefreshFilters();
            }
        }
    }
}
