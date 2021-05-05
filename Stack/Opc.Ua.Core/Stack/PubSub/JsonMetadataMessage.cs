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
    public class JsonDataSetMetaData
    {
        /// <summary>
        /// 
        /// </summary>
        public string MessageId { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string MessageType { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string PublisherId { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string DataSetClassId { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public DataSetMetaDataType MetaData { get; set; }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="context"></param>
        /// <param name="useReversibleEncoding"></param>
        /// <param name="writer"></param>
        public void Encode(ServiceMessageContext context, bool useReversibleEncoding, StreamWriter writer)
        {
            using (JsonEncoder encoder = new JsonEncoder(context, useReversibleEncoding, writer, false))
            {
                encoder.WriteString("MessageId", MessageId);
                encoder.WriteString("MessageType", "ua-metadata");
                encoder.WriteString("PublisherId", PublisherId);
                encoder.WriteString("DataSetClassId", DataSetClassId);
                encoder.WriteEncodeable("MetaData", MetaData, typeof(DataSetMetaDataType));

                encoder.Close();
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="context"></param>
        /// <param name="reader"></param>
        /// <returns></returns>
        public static JsonDataSetMetaData Decode(ServiceMessageContext context, StreamReader reader)
        {
            var json = reader.ReadToEnd();

            JsonDataSetMetaData output = new JsonDataSetMetaData();

            using (JsonDecoder decoder = new JsonDecoder(json, context))
            {
                output.MessageId = decoder.ReadString(nameof(output.MessageId));
                output.MessageType = decoder.ReadString("MessageType");
                output.PublisherId = decoder.ReadString("PublisherId");
                output.DataSetClassId = decoder.ReadString("DataSetClassId");
                output.MetaData = (DataSetMetaDataType)decoder.ReadEncodeable("MetaData", typeof(DataSetMetaDataType));

                decoder.Close();
            }

            return output;
        }
    }
}
