/* Copyright (c) 1996-2022 The OPC Foundation. All rights reserved.
   The source code in this file is covered under a dual-license scenario:
     - RCL: for OPC Foundation Corporate Members in good-standing
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
using System.Runtime.Serialization;
using System.Xml;

namespace Opc.Ua
{
    /// <summary>
	/// A concrete base class used by the autogenerated code.
	/// </summary>
    [DataContract(Name = "EncodeableObject", Namespace = Namespaces.OpcUaXsd)]
    public abstract class EncodeableObject : IEncodeable
    {
        #region IEncodeable Methods
        /// <inheritdoc/>
        public abstract ExpandedNodeId TypeId { get; }

        /// <inheritdoc/>
        public abstract ExpandedNodeId BinaryEncodingId { get; }

        /// <inheritdoc/>
        public abstract ExpandedNodeId XmlEncodingId { get; }

        /// <inheritdoc/>
        public virtual void Encode(IEncoder encoder) { }

        /// <inheritdoc/>
		public virtual void Decode(IDecoder decoder) { }

        /// <summary>
        /// Checks if the value has changed.
        /// </summary>
        public virtual bool IsEqual(IEncodeable encodeable)
        {
            throw new NotImplementedException("Subclass must implement this method.");
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Applies the data encoding to the value.
        /// </summary>
        public static ServiceResult ApplyDataEncoding(IServiceMessageContext context, QualifiedName dataEncoding, ref object value)
        {
            // check if nothing to do.
            if (QualifiedName.IsNull(dataEncoding) || value == null)
            {
                return ServiceResult.Good;
            }

            // check for supported encoding type.
            if (dataEncoding.NamespaceIndex != 0)
            {
                return StatusCodes.BadDataEncodingUnsupported;
            }

            bool useXml = dataEncoding.Name == BrowseNames.DefaultXml;

            if (!useXml && dataEncoding.Name != BrowseNames.DefaultBinary)
            {
                return StatusCodes.BadDataEncodingInvalid;
            }

            try
            {
                // check for array of encodeables.
                IList<IEncodeable> encodeables = value as IList<IEncodeable>;

                if (encodeables == null)
                {
                    // check for array of extension objects.
                    IList<ExtensionObject> extensions = value as IList<ExtensionObject>;

                    if (extensions != null)
                    {
                        // convert extension objects to encodeables.
                        encodeables = new IEncodeable[extensions.Count];

                        for (int ii = 0; ii < encodeables.Count; ii++)
                        {
                            if (ExtensionObject.IsNull(extensions[ii]))
                            {
                                encodeables[ii] = null;
                                continue;
                            }

                            IEncodeable element = extensions[ii].Body as IEncodeable;

                            if (element == null)
                            {
                                return StatusCodes.BadTypeMismatch;
                            }

                            encodeables[ii] = element;
                        }
                    }
                }

                // apply data encoding to the array.
                if (encodeables != null)
                {
                    ExtensionObject[] extensions = new ExtensionObject[encodeables.Count];

                    for (int ii = 0; ii < extensions.Length; ii++)
                    {
                        extensions[ii] = Encode(context, encodeables[ii], useXml);
                    }

                    value = extensions;
                    return ServiceResult.Good;
                }

                // check for scalar value.
                IEncodeable encodeable = value as IEncodeable;

                if (encodeable == null)
                {
                    ExtensionObject extension = value as ExtensionObject;

                    if (extension == null)
                    {
                        return StatusCodes.BadDataEncodingUnsupported;
                    }

                    encodeable = extension.Body as IEncodeable;
                }

                if (encodeable == null)
                {
                    return StatusCodes.BadDataEncodingUnsupported;
                }

                // do conversion.
                value = Encode(context, encodeable, useXml);
                return ServiceResult.Good;
            }
            catch (Exception e)
            {
                return ServiceResult.Create(e, StatusCodes.BadTypeMismatch, "Could not convert value to requested format.");
            }
        }

        /// <summary>
        /// Encodes the object in XML or Binary
        /// </summary>
        public static ExtensionObject Encode(IServiceMessageContext context, IEncodeable encodeable, bool useXml)
        {
            if (useXml)
            {
                XmlElement body = EncodeableObject.EncodeXml(encodeable, context);
                return new ExtensionObject(encodeable.XmlEncodingId, body);
            }
            else
            {
                byte[] body = EncodeableObject.EncodeBinary(encodeable, context);
                return new ExtensionObject(encodeable.BinaryEncodingId, body);
            }
        }

        /// <summary>
        /// Encodes the object in XML.
        /// </summary>
        public static XmlElement EncodeXml(IEncodeable encodeable, IServiceMessageContext context)
        {
            // create encoder.
            using (XmlEncoder encoder = new XmlEncoder(context))
            {
                // write body.
                encoder.WriteExtensionObjectBody(encodeable);

                // create document from encoder.
                XmlDocument document = new XmlDocument();
                document.LoadInnerXml(encoder.CloseAndReturnText());

                // return root element.
                return document.DocumentElement;
            }
        }

        /// <summary>
        /// Encodes the object in binary
        /// </summary>
        public static byte[] EncodeBinary(IEncodeable encodeable, IServiceMessageContext context)
        {
            BinaryEncoder encoder = new BinaryEncoder(context);
            encoder.WriteEncodeable(null, encodeable, null);
            return encoder.CloseAndReturnBuffer();
        }
        #endregion

        #region ICloneable
        /// <inheritdoc/>
        public virtual object Clone()
        {
            return this.MemberwiseClone();
        }

        /// <summary>
        /// Returns a deep copy of an encodeable object.
        /// </summary>
        public new object MemberwiseClone()
        {
            return base.MemberwiseClone();
        }
        #endregion
    }
}
