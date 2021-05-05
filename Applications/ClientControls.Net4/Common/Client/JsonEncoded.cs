using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Newtonsoft.Json;
using Opc.Ua.PubSub;
using Opc.Ua.PubSub.Encoding;
using Opc.Ua.PubSub.PublishedData;

namespace Opc.Ua.Client.Controls
{
    public partial class JsonEncoded : Form
    {
        public JsonEncoded()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Prompts the user to view or edit the value.
        /// </summary>
        public object ShowDialog(
            Session session,
            NodeId nodeId,
            uint attributeId,
            object value,
            string caption)
        {
            if (!String.IsNullOrEmpty(caption))
            {
                this.Text = caption;
            }

            Context = session.MessageContext;
            DataValues = new DataValue[] { (DataValue)value };
            NodeId = nodeId;

            UpdateJson();

            if (base.ShowDialog() != DialogResult.OK)
            {
                return null;
            }

            return null;
        }


        public DataValue[] DataValues { get; set; }
        public ServiceMessageContext Context { get; set; }
        public NodeId NodeId { get; set; }

        private void NetworkMessageHeader_CheckedChanged(object sender, EventArgs e)
        {
            UpdateJson();
        }

        private void DataSetMessageHeader_CheckedChanged(object sender, EventArgs e)
        {
            UpdateJson();
        }

        private void SingleDataMessage_CheckedChanged(object sender, EventArgs e)
        {
            UpdateJson();
        }

        private void PublisherId_CheckedChanged(object sender, EventArgs e)
        {
            UpdateJson();
        }

        private void DatasetClassId_CheckedChanged(object sender, EventArgs e)
        {
            UpdateJson();
        }

        private void ReplyTo_CheckedChanged(object sender, EventArgs e)
        {
            UpdateJson();
        }

        private void DatasetWriterId_CheckedChanged(object sender, EventArgs e)
        {
            UpdateJson();
        }

        private void MetadataVersion_CheckedChanged(object sender, EventArgs e)
        {
            UpdateJson();
        }

        private void SequenceNumber_CheckedChanged(object sender, EventArgs e)
        {
            UpdateJson();
        }

        private void Timestamp_CheckedChanged(object sender, EventArgs e)
        {
            UpdateJson();
        }

        private void Status_CheckedChanged(object sender, EventArgs e)
        {
            UpdateJson();
        }

        private void StatusCode_CheckedChanged(object sender, EventArgs e)
        {
            UpdateJson();
        }

        private void SourceTimestamp_CheckedChanged(object sender, EventArgs e)
        {
            UpdateJson();
        }

        private void ServerTimestamp_CheckedChanged(object sender, EventArgs e)
        {
            UpdateJson();
        }

        private void SourcePicoseconds_CheckedChanged(object sender, EventArgs e)
        {
            UpdateJson();
        }

        private void ServerPicoseconds_CheckedChanged(object sender, EventArgs e)
        {
            UpdateJson();
        }

        private void RawData_CheckedChanged(object sender, EventArgs e)
        {
            UpdateJson();
        }

        private void SimDataValue_CheckedChanged(object sender, EventArgs e)
        {
            UpdateJson();
        }

        private void Reversible_CheckedChanged(object sender, EventArgs e)
        {
            UpdateJson();
        }

        private JsonNetworkMessageContentMask jsonNetworkMessageContentMask;
        private JsonDataSetMessageContentMask jsonDataSetMessageContentMask;
        private DataSetFieldContentMask dataSetFieldContentMask;
        private bool pingpong;

        private void UpdateJson()
        {
            if (pingpong)
            {
                UpdateJson1();
                this.Text = "Microsoft";
            }
            else
            {
                UpdateJson2();
                this.Text = "Softing";
            }

            pingpong = !pingpong;
        }

        private void UpdateJson1()
        {
            UpdateFlags();

            var networkMessage = new Opc.Ua.PubSub.JsonNetworkMessage() {
                MessageId = Guid.NewGuid().ToString(),
                DataSetClassId = Guid.NewGuid().ToString(),
                PublisherId = Guid.NewGuid().ToString(),
                MessageContentMask = jsonNetworkMessageContentMask
            };

            for (int i = 0; i < 2; i++)
            {
                var dataSetMessage = new Opc.Ua.PubSub.JsonDataSetMessage() {
                    SequenceNumber = (uint)(123 + i),
                    DataSetWriterId = "5555",
                    MetaDataVersion = new ConfigurationVersionDataType() { MajorVersion = 1, MinorVersion = 0 },
                    Timestamp = DateTime.UtcNow + TimeSpan.FromMilliseconds(i * 100),
                    Status = new StatusCode(StatusCodes.Uncertain),
                    FieldContentMask = dataSetFieldContentMask,
                    MessageContentMask = jsonDataSetMessageContentMask
                };

                foreach (var dataValue in DataValues)
                {
                    DataValue clonedDataValue = (DataValue)dataValue.MemberwiseClone();
                    if (SimDataValue.Checked)
                    {
                        clonedDataValue.StatusCode = (i & 1) == 0 ? StatusCodes.Uncertain : StatusCodes.Good;
                        clonedDataValue.SourceTimestamp = DateTime.UtcNow + TimeSpan.FromMilliseconds(i * 10);
                        clonedDataValue.ServerTimestamp = DateTime.UtcNow - TimeSpan.FromSeconds(123 + i);
                        clonedDataValue.SourcePicoseconds = (ushort)(123 + i);
                        clonedDataValue.ServerPicoseconds = (ushort)(456 + i);
                    }

                    dataSetMessage.Payload[$"#0"] = (DataValue)clonedDataValue.MemberwiseClone();

                    if (SimDataValue.Checked)
                    {
                        clonedDataValue.StatusCode = (i & 1) == 0 ? StatusCodes.Good : StatusCodes.Bad;
                        clonedDataValue.SourceTimestamp = DateTime.UtcNow + TimeSpan.FromMilliseconds(i * 10);
                        clonedDataValue.ServerTimestamp = DateTime.UtcNow - TimeSpan.FromSeconds(123 + i);
                        clonedDataValue.SourcePicoseconds = (ushort)(123 + i);
                        clonedDataValue.ServerPicoseconds = (ushort)(456 + i);
                    }

                    dataSetMessage.Payload[$"#1"] = clonedDataValue;
                }

                networkMessage.Messages.Add(dataSetMessage);
            }

            using (MemoryStream stream = new MemoryStream(1024))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 65535, true))
            {
                networkMessage.Encode(Context, writer);
                var encoded = Encoding.UTF8.GetString(stream.ToArray());

                var prettyText = PrettifyAndValidateJson(encoded);

                JsonOutput.Text = prettyText;
            }

        }
        private void UpdateJson2()
        {
            UpdateFlags();

            // why UaDataSetMessage? Why different namespace?
            var dataSetMessageList = new List<Opc.Ua.PubSub.UaDataSetMessage>();
            for (int i = 0; i < 2; i++)
            {
                var dataSetList = new List<DataSet>();
                foreach (var dataValue in DataValues)
                {
                    DataValue clonedDataValue = (DataValue)dataValue.MemberwiseClone();
                    if (SimDataValue.Checked)
                    {
                        clonedDataValue.StatusCode = (i & 1) == 0 ? StatusCodes.Uncertain : StatusCodes.Good;
                        clonedDataValue.SourceTimestamp = DateTime.UtcNow + TimeSpan.FromMilliseconds(i * 10);
                        clonedDataValue.ServerTimestamp = DateTime.UtcNow - TimeSpan.FromSeconds(123 + i);
                        clonedDataValue.SourcePicoseconds = (ushort)(123 + i);
                        clonedDataValue.ServerPicoseconds = (ushort)(456 + i);
                    }

                    //dataSetMessage.Payload[$"#0"] =

                    var fields = new List<Field>();
                    fields.Add(new Field() {
                        Value = (DataValue)clonedDataValue.MemberwiseClone(),
                        FieldMetaData = new FieldMetaData() { Name = $"#0" }
                    });

                    if (SimDataValue.Checked)
                    {
                        clonedDataValue.StatusCode = (i & 1) == 0 ? StatusCodes.Good : StatusCodes.Bad;
                        clonedDataValue.SourceTimestamp = DateTime.UtcNow + TimeSpan.FromMilliseconds(i * 10);
                        clonedDataValue.ServerTimestamp = DateTime.UtcNow - TimeSpan.FromSeconds(123 + i);
                        clonedDataValue.SourcePicoseconds = (ushort)(123 + i);
                        clonedDataValue.ServerPicoseconds = (ushort)(456 + i);
                    }

                    fields.Add(new Field() {
                        Value = (DataValue)clonedDataValue.MemberwiseClone(),
                        FieldMetaData = new FieldMetaData() { Name = $"#1" }
                    });

                    //dataSetMessage.Payload[$"#1"] = clonedDataValue;

                    var dataSet = new DataSet();
                    dataSet.Fields = fields.ToArray();

                    var dataSetMessage = new Opc.Ua.PubSub.Encoding.JsonDataSetMessage(dataSet) {
                        SequenceNumber = (uint)(123 + i),
                        DataSetWriterId = 5555,
                        MetaDataVersion = new ConfigurationVersionDataType() { MajorVersion = 1, MinorVersion = 0 },
                        Timestamp = DateTime.UtcNow + TimeSpan.FromMilliseconds(i * 100),
                        Status = new StatusCode(StatusCodes.Uncertain)
                    };
                    dataSetMessage.SetMessageContentMask(jsonDataSetMessageContentMask);
                    dataSetMessage.SetFieldContentMask(dataSetFieldContentMask);

                    dataSetMessageList.Add(dataSetMessage);
                }
            }

            var networkMessage = new Opc.Ua.PubSub.Encoding.JsonNetworkMessage(null, dataSetMessageList) {
                MessageId = Guid.NewGuid().ToString(),
                DataSetClassId = Guid.NewGuid().ToString(),
                PublisherId = Guid.NewGuid().ToString()
            };
            networkMessage.SetNetworkMessageContentMask(jsonNetworkMessageContentMask);

            using (MemoryStream stream = new MemoryStream(1024))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 65535, true))
            {
                networkMessage.Encode(Context, writer);
                var encoded = Encoding.UTF8.GetString(stream.ToArray());

                var prettyText = PrettifyAndValidateJson(encoded);

                JsonOutput.Text = prettyText;
            }
        }

        private void UpdateFlags()
        {
            jsonNetworkMessageContentMask = 0;
            if (NetworkMessageHeader.Checked) jsonNetworkMessageContentMask |= JsonNetworkMessageContentMask.NetworkMessageHeader;
            if (DataSetMessageHeader.Checked) jsonNetworkMessageContentMask |= JsonNetworkMessageContentMask.DataSetMessageHeader;
            if (SingleDataMessage.Checked) jsonNetworkMessageContentMask |= JsonNetworkMessageContentMask.SingleDataSetMessage;
            if (PublisherId.Checked) jsonNetworkMessageContentMask |= JsonNetworkMessageContentMask.PublisherId;
            if (DatasetClassId.Checked) jsonNetworkMessageContentMask |= JsonNetworkMessageContentMask.DataSetClassId;
            if (ReplyTo.Checked) jsonNetworkMessageContentMask |= JsonNetworkMessageContentMask.ReplyTo;

            jsonDataSetMessageContentMask = 0;
            if (DatasetWriterId.Checked) jsonDataSetMessageContentMask |= JsonDataSetMessageContentMask.DataSetWriterId;
            if (MetadataVersion.Checked) jsonDataSetMessageContentMask |= JsonDataSetMessageContentMask.MetaDataVersion;
            if (SequenceNumber.Checked) jsonDataSetMessageContentMask |= JsonDataSetMessageContentMask.SequenceNumber;
            if (Status.Checked) jsonDataSetMessageContentMask |= JsonDataSetMessageContentMask.Status;
            if (Timestamp.Checked) jsonDataSetMessageContentMask |= JsonDataSetMessageContentMask.Timestamp;

            dataSetFieldContentMask = 0;
            if (RawData.Checked) dataSetFieldContentMask |= DataSetFieldContentMask.RawData;
            if (Reversible.Checked) dataSetFieldContentMask |= DataSetFieldContentMask.Reversible;
            if (SourceTimestamp.Checked) dataSetFieldContentMask |= DataSetFieldContentMask.SourceTimestamp;
            if (ServerTimestamp.Checked) dataSetFieldContentMask |= DataSetFieldContentMask.ServerTimestamp;
            if (SourcePicoseconds.Checked) dataSetFieldContentMask |= DataSetFieldContentMask.SourcePicoSeconds;
            if (ServerPicoseconds.Checked) dataSetFieldContentMask |= DataSetFieldContentMask.ServerPicoSeconds;
            if (StatusCode.Checked) dataSetFieldContentMask |= DataSetFieldContentMask.StatusCode;
        }

        /// <summary>
        /// Format and validate a JSON string.
        /// </summary>
        protected string PrettifyAndValidateJson(string json)
        {
            try
            {
                using (var stringWriter = new StringWriter())
                using (var stringReader = new StringReader(json))
                {
                    var jsonReader = new JsonTextReader(stringReader);
                    var jsonWriter = new JsonTextWriter(stringWriter) {
                        Formatting = Newtonsoft.Json.Formatting.Indented,
                        Culture = System.Globalization.CultureInfo.InvariantCulture
                    };
                    jsonWriter.WriteToken(jsonReader);
                    string formattedJson = stringWriter.ToString();
                    return formattedJson;
                }
            }
#pragma warning disable RCS1075 // Avoid empty catch clause that catches System.Exception.
            catch (Exception)
#pragma warning restore RCS1075 // Avoid empty catch clause that catches System.Exception.
            {
            }
            return json;
        }

        private void OkBtn_Click(object sender, EventArgs e)
        {

        }
    }
}
