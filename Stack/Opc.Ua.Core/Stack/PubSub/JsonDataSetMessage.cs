/* Copyright (c) 1996-2020 The OPC Foundation. All rights reserved.
   The source code in this file is covered under a dual-license scenario:
     - RCL: for OPC Foundation members in good-standing
     - GPL V2: everybody else
   RCL license terms accompanied with this source code. See http://opcfoundation.org/License/RCL/1.00/
   GNU General Public License as published by the Free Software Foundation;
   version 2 of the License are accompanied with this source code. See http://opcfoundation.org/License/GPLv2
   This source code is distributed in the hope that it will be useful,
   but WITHOUT ANY WARRANTY; without even the implied warranty of
   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
*/



using System;
using System.Collections.Generic;
using System.IO;
using Opc.Ua;

namespace Opc.Ua.PubSub
{
    /// <summary>
    /// 
    /// </summary>
    public class JsonDataSetMessage : IEncodeable
    {
        public JsonDataSetMessage()
        {
            MessageContentMask =
                JsonDataSetMessageContentMask.DataSetWriterId |
                JsonDataSetMessageContentMask.MetaDataVersion |
                JsonDataSetMessageContentMask.SequenceNumber;

            FieldContentMask = (DataSetFieldContentMask)0;

            Payload = new Dictionary<string, DataValue>();
        }

        public JsonDataSetMessageContentMask MessageContentMask { get; set; }

        public string DataSetWriterId { get; set; }

        public uint SequenceNumber { get; set; }

        public ConfigurationVersionDataType MetaDataVersion { get; set; }

        public DateTime Timestamp { get; set; }

        public StatusCode Status { get; set; }

        public DataSetFieldContentMask FieldContentMask { get; set; }

        public Dictionary<string, DataValue> Payload { get; set; }

        public ExpandedNodeId TypeId { get { return ExpandedNodeId.Null; } }

        public ExpandedNodeId BinaryEncodingId { get { return ExpandedNodeId.Null; } }

        public ExpandedNodeId XmlEncodingId { get { return ExpandedNodeId.Null; } }

        private void EncodeField(JsonEncoder encoder, string fieldName, DataValue value)
        {
            if (FieldContentMask == 0)
            {
                // value encoded as reversible
                encoder.WriteVariant(fieldName, value.WrappedValue, true);
                return;
            }

            if ((FieldContentMask & DataSetFieldContentMask.RawData) != 0)
            {
                // switch to non reversible here ?
                // https://reference.opcfoundation.org/v104/Core/docs/Part14/7.2.3/

                var variant = value.WrappedValue;

                if (variant.TypeInfo == null || variant.TypeInfo.BuiltInType == BuiltInType.Null)
                {
                    return;
                }

                encoder.WriteVariant(fieldName, variant, false);
                return;
            }

            bool reversible = (FieldContentMask & DataSetFieldContentMask.Reversible) != 0;

            DataValue dv = new DataValue();
            dv.WrappedValue = value.WrappedValue;

            if ((FieldContentMask & DataSetFieldContentMask.StatusCode) != 0)
            {
                dv.StatusCode = value.StatusCode;
            }

            if ((FieldContentMask & DataSetFieldContentMask.SourceTimestamp) != 0)
            {
                dv.SourceTimestamp = value.SourceTimestamp;
            }

            if ((FieldContentMask & DataSetFieldContentMask.SourcePicoSeconds) != 0)
            {
                dv.SourcePicoseconds = value.SourcePicoseconds;
            }

            if ((FieldContentMask & DataSetFieldContentMask.ServerTimestamp) != 0)
            {
                dv.ServerTimestamp = value.ServerTimestamp;
            }

            if ((FieldContentMask & DataSetFieldContentMask.ServerPicoSeconds) != 0)
            {
                dv.ServerPicoseconds = value.ServerPicoseconds;
            }

            // DataValue is non reversible
            // https://reference.opcfoundation.org/v104/Core/docs/Part14/7.2.3/
            encoder.WriteDataValue(fieldName, dv, reversible);
        }

        public void Encode(JsonEncoder encoder, JsonNetworkMessageContentMask messageContentMask)
        {
            bool singleDataSetMessage = (messageContentMask & JsonNetworkMessageContentMask.SingleDataSetMessage) != 0;
            if ((messageContentMask & JsonNetworkMessageContentMask.DataSetMessageHeader) != 0)
            {
                bool networkMessageHeader = (messageContentMask & JsonNetworkMessageContentMask.NetworkMessageHeader) != 0;
                if (!networkMessageHeader && !singleDataSetMessage)
                {
                    encoder.PushStructure(null);
                }
                Encode(encoder);
                if (!networkMessageHeader && !singleDataSetMessage)
                {
                    encoder.PopStructure();
                }
                return;
            }

            if (!singleDataSetMessage)
            {
                encoder.PushStructure(null);
            }
            if (Payload != null)
            {
                foreach (var ii in Payload)
                {
                    EncodeField(encoder, ii.Key, ii.Value);
                }
            }
            else
            {
                // write null
            }
            if (!singleDataSetMessage)
            {
                encoder.PopStructure();
            }
        }

        void IEncodeable.Encode(IEncoder encoder)
        {
            Encode((JsonEncoder)encoder);
        }

        public void Encode(JsonEncoder encoder)
        {
            if ((MessageContentMask & JsonDataSetMessageContentMask.DataSetWriterId) != 0)
            {
                encoder.WriteString(nameof(DataSetWriterId), DataSetWriterId);
            }
            else
            {
                encoder.WriteString(nameof(DataSetWriterId), null);
            }

            if ((MessageContentMask & JsonDataSetMessageContentMask.SequenceNumber) != 0)
            {
                encoder.WriteUInt32(nameof(SequenceNumber), SequenceNumber);
            }
            else
            {
                encoder.WriteUInt32(nameof(SequenceNumber), 0);
            }

            if ((MessageContentMask & JsonDataSetMessageContentMask.MetaDataVersion) != 0)
            {
                encoder.WriteEncodeable("MetaDataVersion", MetaDataVersion, typeof(ConfigurationVersionDataType));
            }
            else
            {
                encoder.WriteEncodeable("MetaDataVersion", null, typeof(ConfigurationVersionDataType));
            }

            if ((MessageContentMask & JsonDataSetMessageContentMask.Timestamp) != 0)
            {
                encoder.WriteDateTime("Timestamp", Timestamp);
            }
            else
            {
                encoder.WriteDateTime("Timestamp", DateTime.MinValue);
            }

            if ((MessageContentMask & JsonDataSetMessageContentMask.Status) != 0)
            {
                encoder.WriteStatusCode("Status", Status);
            }
            else
            {
                encoder.WriteStatusCode("Status", StatusCodes.Good);
            }

            if (Payload != null)
            {
                ((JsonEncoder)encoder).PushStructure("Payload");

                foreach (var ii in Payload)
                {
                    EncodeField(encoder, ii.Key, ii.Value);
                }

                ((JsonEncoder)encoder).PopStructure();
            }
        }

        public void Decode(IDecoder decoder)
        {
            throw new NotImplementedException();
        }

        public bool IsEqual(IEncodeable encodeable)
        {
            if (Object.ReferenceEquals(this, encodeable))
            {
                return true;
            }

            // TODO

            return false;
        }
    }


}
