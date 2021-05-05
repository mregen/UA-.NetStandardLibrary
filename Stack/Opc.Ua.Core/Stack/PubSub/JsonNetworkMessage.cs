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

    public class JsonNetworkMessage
    {
        public JsonNetworkMessage()
        {
            MessageContentMask =
                JsonNetworkMessageContentMask.NetworkMessageHeader |
                JsonNetworkMessageContentMask.DataSetMessageHeader |
                JsonNetworkMessageContentMask.PublisherId |
                JsonNetworkMessageContentMask.DataSetClassId;

            Messages = new List<JsonDataSetMessage>();

            MessageType = "ua-data";

        }

        public JsonNetworkMessageContentMask MessageContentMask { get; set; }

        public string MessageId { get; set; }

        public string MessageType { get; private set; }

        public string PublisherId { get; set; }

        public string DataSetClassId { get; set; }

        public string ReplyTo { get; set; }

        public List<JsonDataSetMessage> Messages { get; set; }

        public void Encode(ServiceMessageContext context, bool useReversibleEncoding, StreamWriter writer)
        {
            bool topLevelIsArray = false;

            if ((MessageContentMask & JsonNetworkMessageContentMask.NetworkMessageHeader) == 0 &&
                (MessageContentMask & JsonNetworkMessageContentMask.SingleDataSetMessage) == 0)
            {
                topLevelIsArray = true;
            }

            using (JsonEncoder encoder = new JsonEncoder(context, useReversibleEncoding, writer, topLevelIsArray) { IncludeDefaultNumberValues = false })
            {
                if ((MessageContentMask & JsonNetworkMessageContentMask.NetworkMessageHeader) != 0)
                {
                    Encode(encoder);
                }
                else if (Messages != null && Messages.Count > 0)
                {
                    if ((MessageContentMask & JsonNetworkMessageContentMask.SingleDataSetMessage) != 0)
                    {
                        encoder.PushStructure(null);
                        Messages[0].Encode(encoder, MessageContentMask);
                        encoder.PopStructure();
                    }
                    else
                    {
                        if (!topLevelIsArray)
                        {
                            encoder.PushArray(null);
                        }
                        foreach (var message in Messages)
                        {
                            message.Encode(encoder, MessageContentMask);
                        }
                        if (!topLevelIsArray)
                        {
                            encoder.PopArray();
                        }
                    }
                }

                encoder.Close();
            }
        }

        protected void Encode(JsonEncoder encoder)
        {
            bool popStructure = false;

            if ((MessageContentMask & JsonNetworkMessageContentMask.NetworkMessageHeader) != 0)
            {
                popStructure = true;
                encoder.PushStructure(null);

                encoder.WriteString("MessageId", MessageId);
                encoder.WriteString("MessageType", MessageType);

                if ((MessageContentMask & JsonNetworkMessageContentMask.PublisherId) != 0)
                {
                    encoder.WriteString("PublisherId", PublisherId);
                }
                else
                {
                    encoder.WriteString("PublisherId", null);
                }

                if ((MessageContentMask & JsonNetworkMessageContentMask.DataSetClassId) != 0)
                {
                    encoder.WriteString("DataSetClassId", DataSetClassId);
                }
                else
                {
                    encoder.WriteString("DataSetClassId", null);
                }
            }

            if (Messages != null && Messages.Count > 0)
            {
                var fieldName = popStructure ? "Messages" : null;
                bool singleDataSetMessage = (MessageContentMask & JsonNetworkMessageContentMask.SingleDataSetMessage) != 0;
                if ((MessageContentMask & JsonNetworkMessageContentMask.DataSetMessageHeader) != 0)
                {
                    if (singleDataSetMessage)
                    {
                        encoder.WriteEncodeable(fieldName, Messages[0], typeof(JsonDataSetMessage));
                    }
                    else
                    {
                        encoder.WriteEncodeableArray(fieldName, Messages.ToArray(), typeof(JsonDataSetMessage));
                    }
                }
                else
                {
                    if (singleDataSetMessage)
                    {
                        encoder.PushStructure(fieldName);
                        Messages[0].Encode(encoder, MessageContentMask);
                        encoder.PopStructure();
                    }
                    else
                    {
                        encoder.PushArray(fieldName);
                        foreach (var message in Messages)
                        {
                            message.Encode(encoder, MessageContentMask);
                        }
                        encoder.PopArray();
                    }
                }
            }

            if (popStructure)
            {
                encoder.PopStructure();
            }
        }
    }

}
