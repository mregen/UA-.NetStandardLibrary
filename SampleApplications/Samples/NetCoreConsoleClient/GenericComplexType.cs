/* Copyright (c) 1996-2019 The OPC Foundation. All rights reserved.
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
using System.Linq;
using System.Runtime.Serialization;
using System.Xml;

namespace Opc.Ua
{
    public class GenericComplexType : IEncodeable, IComplexTypeInstance
    {
        #region Constructors
        /// <summary>
        /// Initializes the object with default values.
        /// </summary>
        /// <remarks>
        /// Initializes the object with default values.
        /// </remarks>
        public GenericComplexType()
        {
            TypeId = ExpandedNodeId.Null;
            m_context = MessageContextExtension.CurrentContext;
        }

        /// <summary>
        /// Initializes the object with a <paramref name="typeId"/>.
        /// </summary>
        /// <param name="typeId">The type to copy and create an instance from</param>
        public GenericComplexType(ExpandedNodeId typeId)
        {
            TypeId = typeId;
        }

        /// <summary>
        /// Initializes the object with a <paramref name="typeId"/>.
        /// </summary>
        /// <param name="typeId">The type to copy and create an instance from</param>
        public GenericComplexType(GenericComplexType complexType)
        {
            TypeId = complexType.TypeId;
        }


        [OnSerializing()]
        private void UpdateContext(StreamingContext context)
        {
            m_context = MessageContextExtension.CurrentContext;
        }

        /// <summary>
        /// Initializes the object during deserialization.
        /// </summary>
        [OnDeserializing()]
        private void Initialize(StreamingContext context)
        {
            TypeId = ExpandedNodeId.Null;
            m_context = MessageContextExtension.CurrentContext;
        }
        #endregion

        #region Public Properties
        /// <summary>
        /// The data type node id for the extension object.
        /// </summary>
        /// <value>The type id.</value>
        public ExpandedNodeId TypeId { get; set; }

        public ExpandedNodeId BinaryEncodingId => throw new NotImplementedException();

        public ExpandedNodeId XmlEncodingId => throw new NotImplementedException();
        #endregion

        #region Overridden Methods
        #endregion

        #region IFormattable Members
        #endregion

        #region ICloneable Members
        /// <summary>
        /// Makes a deep copy of the object.
        /// </summary>
        /// <returns>
        /// A new object that is a copy of this instance.
        /// </returns>
        public new object MemberwiseClone()
        {
            // TODO: how to create properties in derived class?
            return new GenericComplexType(this);
        }

        public void Encode(IEncoder encoder)
        {
            throw new NotImplementedException();
        }

        public void Decode(IDecoder decoder)
        {
            decoder.PushNamespace(TypeId.NamespaceUri);

            var properties = GetType().GetProperties();
            foreach (var property in properties)
            {
                if (property.CustomAttributes.Count() == 0)
                {
                    continue;
                }

                if (property.PropertyType == typeof(Boolean))
                {
                    property.SetValue(this, decoder.ReadBoolean(property.Name));
                }
                else if (property.PropertyType == typeof(SByte))
                {
                    property.SetValue(this, decoder.ReadSByte(property.Name));
                }
                else if (property.PropertyType == typeof(Byte))
                {
                    property.SetValue(this, decoder.ReadByte(property.Name));
                }
                else if (property.PropertyType == typeof(Int16))
                {
                    property.SetValue(this, decoder.ReadInt16(property.Name));
                }
                else if (property.PropertyType == typeof(UInt16))
                {
                    property.SetValue(this, decoder.ReadUInt16(property.Name));
                }
                else if (property.PropertyType == typeof(Int32))
                {
                    property.SetValue(this, decoder.ReadInt32(property.Name));
                }
                else if (property.PropertyType.IsEnum)
                {
                    property.SetValue(this, decoder.ReadEnumerated(property.Name, property.PropertyType));
                }
                else if (property.PropertyType == typeof(UInt32))
                {
                    property.SetValue(this, decoder.ReadUInt32(property.Name));
                }
                else if (property.PropertyType == typeof(Int64))
                {
                    property.SetValue(this, decoder.ReadInt64(property.Name));
                }
                else if (property.PropertyType == typeof(UInt64))
                {
                    property.SetValue(this, decoder.ReadUInt64(property.Name));
                }
                else if (property.PropertyType == typeof(Single))
                {
                    property.SetValue(this, decoder.ReadFloat(property.Name));
                }
                else if (property.PropertyType == typeof(Double))
                {
                    property.SetValue(this, decoder.ReadDouble(property.Name));
                }
                else if (property.PropertyType == typeof(String))
                {
                    property.SetValue(this, decoder.ReadString(property.Name));
                }
                else if (property.PropertyType == typeof(DateTime))
                {
                    property.SetValue(this, decoder.ReadDateTime(property.Name));
                }
                else if (property.PropertyType == typeof(Uuid))
                {
                    property.SetValue(this, decoder.ReadGuid(property.Name));
                }
                else if (property.PropertyType == typeof(Byte[]))
                {
                    property.SetValue(this, decoder.ReadByteArray(property.Name));
                }
                else if (property.PropertyType == typeof(XmlElement))
                {
                    property.SetValue(this, decoder.ReadXmlElement(property.Name));
                }
                else if (property.PropertyType == typeof(NodeId))
                {
                    property.SetValue(this, decoder.ReadNodeId(property.Name));
                }
                else if (property.PropertyType == typeof(ExpandedNodeId))
                {
                    property.SetValue(this, decoder.ReadExpandedNodeId(property.Name));
                }
                else if (property.PropertyType == typeof(StatusCode))
                {
                    property.SetValue(this, decoder.ReadStatusCode(property.Name));
                }
                else if (property.PropertyType == typeof(DiagnosticInfo))
                {
                    property.SetValue(this, decoder.ReadDiagnosticInfo(property.Name));
                }
                else if (property.PropertyType == typeof(QualifiedName))
                {
                    property.SetValue(this, decoder.ReadQualifiedName(property.Name));
                }
                else if (property.PropertyType == typeof(LocalizedText))
                {
                    property.SetValue(this, decoder.ReadLocalizedText(property.Name));
                }
                else if (property.PropertyType == typeof(DataValue))
                {
                    property.SetValue(this, decoder.ReadDataValue(property.Name));
                }
                else if (property.PropertyType == typeof(Variant))
                {
                    property.SetValue(this, decoder.ReadVariant(property.Name));
                }
                else if (property.PropertyType == typeof(ExtensionObject))
                {
                    property.SetValue(this, decoder.ReadExtensionObject(property.Name));
                }
                else if (property.PropertyType is IEncodeable)
                {
                    property.SetValue(this, decoder.ReadEncodeable(property.Name, property.PropertyType));
                }
                else
                {
                    throw new NotImplementedException();
                }
            }

            decoder.PopNamespace();

        }

        public bool IsEqual(IEncodeable encodeable)
        {
            if (Object.ReferenceEquals(this, encodeable))
            {
                return true;
            }

            var myType = GetType();
            var value = encodeable as GenericComplexType;
            if (value == null)
            {
                return false;
            }

            // TODO: full compare

            return true;
        }
        #endregion

        #region Static Members
        #endregion

        #region Private Members
        #endregion

        #region Private Fields
        private ServiceMessageContext m_context;
        #endregion
    }
}//namespace
