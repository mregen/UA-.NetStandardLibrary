/* ========================================================================
 * Copyright (c) 2005-2019 The OPC Foundation, Inc. All rights reserved.
 *
 * OPC Foundation MIT License 1.00
 * 
 * Permission is hereby granted, free of charge, to any person
 * obtaining a copy of this software and associated documentation
 * files (the "Software"), to deal in the Software without
 * restriction, including without limitation the rights to use,
 * copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the
 * Software is furnished to do so, subject to the following
 * conditions:
 * 
 * The above copyright notice and this permission notice shall be
 * included in all copies or substantial portions of the Software.
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
 * EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
 * OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
 * NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
 * HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
 * WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
 * OTHER DEALINGS IN THE SOFTWARE.
 *
 * The complete license agreement can be found here:
 * http://opcfoundation.org/License/MIT/1.00/
 * ======================================================================*/


using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml;

namespace Opc.Ua.Client.ComplexTypes
{
    public class BaseComplexType : IEncodeable, IComplexTypeInstance
    {
        #region Constructors
        /// <summary>
        /// Initializes the object with default values.
        /// </summary>
        /// <remarks>
        /// Initializes the object with default values.
        /// </remarks>
        public BaseComplexType()
        {
            TypeId = ExpandedNodeId.Null;
            m_context = MessageContextExtension.CurrentContext;
        }

        /// <summary>
        /// Initializes the object with a <paramref name="typeId"/>.
        /// </summary>
        /// <param name="typeId">The type to copy and create an instance from</param>
        public BaseComplexType(ExpandedNodeId typeId)
        {
            TypeId = typeId;
        }

        /// <summary>
        /// Initializes the object with a <paramref name="typeId"/>.
        /// </summary>
        /// <param name="typeId">The type to copy and create an instance from</param>
        public BaseComplexType(BaseComplexType complexType)
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

        public ExpandedNodeId BinaryEncodingId { get; set; }

        public ExpandedNodeId XmlEncodingId => throw new NotImplementedException();
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
            return new BaseComplexType(this);
        }

        public void Encode(IEncoder encoder)
        {
            encoder.PushNamespace(TypeId.NamespaceUri);

            var attribute = (StructureDefinitionAttribute)
                GetType().GetCustomAttribute(typeof(StructureDefinitionAttribute));

            var properties = GetType().GetProperties();
            if (attribute.StructureType == StructureType.StructureWithOptionalFields)
            {
                UInt32 optionalFields = 0;
                UInt32 bitSelector = 1;
                foreach (var property in properties)
                {
                    var fieldAttribute = (StructureFieldAttribute)
                        property.GetCustomAttribute(typeof(StructureFieldAttribute));

                    if (fieldAttribute == null)
                    {
                        continue;
                    }

                    if (fieldAttribute.IsOptional)
                    {
                        if (property.GetValue(this) != null)
                        {
                            optionalFields |= bitSelector;
                        }
                        bitSelector <<= 1;
                    }
                }

                encoder.WriteUInt32("OptionalField", optionalFields);

                bitSelector = 1;
                foreach (var property in properties)
                {
                    var fieldAttribute = (StructureFieldAttribute)
                        property.GetCustomAttribute(typeof(StructureFieldAttribute));

                    if (fieldAttribute == null)
                    {
                        continue;
                    }

                    if (fieldAttribute.IsOptional)
                    {
                        if ((optionalFields & bitSelector) != 0)
                        {
                            EncodeProperty(encoder, property);
                        }
                        bitSelector <<= 1;
                    }
                }
            }
            else if (attribute.StructureType == StructureType.Union)
            {
                UInt32 unionSelector = 1;
                PropertyInfo unionProperty = null;
                foreach (var property in properties)
                {
                    if (property.CustomAttributes.Count() == 0)
                    {
                        continue;
                    }

                    if (property.GetValue(this) != null)
                    {
                        unionProperty = property;
                        break;
                    }
                    unionSelector++;
                }

                if (unionProperty != null)
                {
                    encoder.WriteUInt32("SwitchField", unionSelector);
                    EncodeProperty(encoder, unionProperty);
                }
                else
                {
                    encoder.WriteUInt32("SwitchField", 0);
                }
            }
            else
            {
                foreach (var property in properties)
                {
                    if (property.CustomAttributes.Count() == 0)
                    {
                        continue;
                    }
                    EncodeProperty(encoder, property);
                }
            }
            encoder.PopNamespace();
        }

        public void Decode(IDecoder decoder)
        {
            decoder.PushNamespace(TypeId.NamespaceUri);

            var attribute = (StructureDefinitionAttribute)
                GetType().GetCustomAttribute(typeof(StructureDefinitionAttribute));

            var properties = GetType().GetProperties();
            if (attribute.StructureType == StructureType.StructureWithOptionalFields)
            {
                UInt32 optionalFields = decoder.ReadUInt32("OptionalField");
                UInt32 bitSelector = 1;
                foreach (var property in properties)
                {
                    var fieldAttribute = (StructureFieldAttribute)
                        property.GetCustomAttribute(typeof(StructureFieldAttribute));

                    if (fieldAttribute == null)
                    {
                        continue;
                    }

                    if (fieldAttribute.IsOptional)
                    {
                        var testSelector = bitSelector;
                        bitSelector <<= 1;
                        if ((testSelector & optionalFields) == 0)
                        {
                            continue;
                        }
                    }

                    DecodeProperty(decoder, property);
                }
            }
            else if (attribute.StructureType == StructureType.Union)
            {
                UInt32 unionSelector = decoder.ReadUInt32("SwitchField");
                if (unionSelector > 0)
                {
                    foreach (var property in properties)
                    {
                        if (property.CustomAttributes.Count() == 0)
                        {
                            continue;
                        }
                        if (--unionSelector == 0)
                        {
                            DecodeProperty(decoder, property);
                            break;
                        }
                    }
                }
            }
            else
            {
                foreach (var property in properties)
                {
                    if (property.CustomAttributes.Count() == 0)
                    {
                        continue;
                    }
                    DecodeProperty(decoder, property);
                }
            }
            decoder.PopNamespace();
        }

        public bool IsEqual(IEncodeable equalValue)
        {
            if (Object.ReferenceEquals(this, equalValue))
            {
                return true;
            }

            var valueBaseType = equalValue as BaseComplexType;
            if (valueBaseType == null)
            {
                return false;
            }

            var valueType = valueBaseType.GetType();
            var thisType = this.GetType();
            if (thisType != valueType)
            {
                return false;
            }

            var valueProps = valueType.GetProperties();
            var valueEnumerator = valueProps.GetEnumerator();
            var properties = thisType.GetProperties();
            foreach (var property in properties)
            {
                if (property.CustomAttributes.Count() == 0)
                {
                    continue;
                }
                PropertyInfo value;
                do
                {
                    value = (PropertyInfo)valueEnumerator.Current;
                    valueEnumerator.MoveNext();
                } while (value.CustomAttributes.Count() == 0);

                if (!Utils.IsEqual(property.GetValue(this), value.GetValue(valueBaseType)))
                {
                    return false;
                }
            }

            // TODO
            // check if second class has been fully compared.

            return true;
        }
        #endregion

        #region Static Members
        #endregion

        #region Private Members
        private void EncodeProperty(IEncoder encoder, PropertyInfo property)
        {
            if (property.PropertyType == typeof(Boolean))
            {
                encoder.WriteBoolean(property.Name, (Boolean)property.GetValue(this));
            }
            else if (property.PropertyType == typeof(SByte))
            {
                encoder.WriteSByte(property.Name, (SByte)property.GetValue(this));
            }
            else if (property.PropertyType == typeof(Byte))
            {
                encoder.WriteByte(property.Name, (Byte)property.GetValue(this));
            }
            else if (property.PropertyType == typeof(Int16))
            {
                encoder.WriteInt16(property.Name, (Int16)property.GetValue(this));
            }
            else if (property.PropertyType == typeof(UInt16))
            {
                encoder.WriteUInt16(property.Name, (UInt16)property.GetValue(this));
            }
            else if (property.PropertyType == typeof(Int32))
            {
                encoder.WriteInt32(property.Name, (Int32)property.GetValue(this));
            }
            else if (property.PropertyType.IsEnum)
            {
                encoder.WriteEnumerated(property.Name, (Enum)property.GetValue(this));
            }
            else if (property.PropertyType == typeof(UInt32))
            {
                encoder.WriteUInt32(property.Name, (UInt32)property.GetValue(this));
            }
            else if (property.PropertyType == typeof(Int64))
            {
                encoder.WriteInt64(property.Name, (Int64)property.GetValue(this));
            }
            else if (property.PropertyType == typeof(UInt64))
            {
                encoder.WriteUInt64(property.Name, (UInt64)property.GetValue(this));
            }
            else if (property.PropertyType == typeof(Single))
            {
                encoder.WriteFloat(property.Name, (Single)property.GetValue(this));
            }
            else if (property.PropertyType == typeof(Double))
            {
                encoder.WriteDouble(property.Name, (Double)property.GetValue(this));
            }
            else if (property.PropertyType == typeof(String))
            {
                encoder.WriteString(property.Name, (String)property.GetValue(this));
            }
            else if (property.PropertyType == typeof(DateTime))
            {
                encoder.WriteDateTime(property.Name, (DateTime)property.GetValue(this));
            }
            else if (property.PropertyType == typeof(Uuid))
            {
                encoder.WriteGuid(property.Name, (Uuid)property.GetValue(this));
            }
            else if (property.PropertyType == typeof(Byte[]))
            {
                encoder.WriteByteArray(property.Name, (Byte[])property.GetValue(this));
            }
            else if (property.PropertyType == typeof(XmlElement))
            {
                encoder.WriteXmlElement(property.Name, (XmlElement)property.GetValue(this));
            }
            else if (property.PropertyType == typeof(NodeId))
            {
                encoder.WriteNodeId(property.Name, (NodeId)property.GetValue(this));
            }
            else if (property.PropertyType == typeof(ExpandedNodeId))
            {
                encoder.WriteExpandedNodeId(property.Name, (ExpandedNodeId)property.GetValue(this));
            }
            else if (property.PropertyType == typeof(StatusCode))
            {
                encoder.WriteStatusCode(property.Name, (StatusCode)property.GetValue(this));
            }
            else if (property.PropertyType == typeof(DiagnosticInfo))
            {
                encoder.WriteDiagnosticInfo(property.Name, (DiagnosticInfo)property.GetValue(this));
            }
            else if (property.PropertyType == typeof(QualifiedName))
            {
                encoder.WriteQualifiedName(property.Name, (QualifiedName)property.GetValue(this));
            }
            else if (property.PropertyType == typeof(LocalizedText))
            {
                encoder.WriteLocalizedText(property.Name, (LocalizedText)property.GetValue(this));
            }
            else if (property.PropertyType == typeof(DataValue))
            {
                encoder.WriteDataValue(property.Name, (DataValue)property.GetValue(this));
            }
            else if (property.PropertyType == typeof(Variant))
            {
                encoder.WriteVariant(property.Name, (Variant)property.GetValue(this));
            }
            else if (property.PropertyType == typeof(ExtensionObject))
            {
                encoder.WriteExtensionObject(property.Name, (ExtensionObject)property.GetValue(this));
            }
            else if (typeof(IEncodeable).IsAssignableFrom(property.PropertyType))
            {
                encoder.WriteEncodeable(property.Name, (IEncodeable)property.GetValue(this), property.PropertyType);
            }
            else
            {
                throw new NotImplementedException($"Unknown type {property.PropertyType} to encode.");
            }
        }

        private void DecodeProperty(IDecoder decoder, PropertyInfo property)
        {
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
            else if (typeof(IEncodeable).IsAssignableFrom(property.PropertyType))
            {
                property.SetValue(this, decoder.ReadEncodeable(property.Name, property.PropertyType));
            }
            else
            {
                throw new NotImplementedException($"Unknown type {property.PropertyType} to decode.");
            }
        }
        #endregion

        #region Private Fields
        private ServiceMessageContext m_context;
        #endregion
    }


}//namespace
