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
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace Opc.Ua.Client.ComplexTypes
{
    public static class DataTypeDefinitionExtension
    {
        public static StructureDefinition ToStructureDefinition(
            this Schema.Binary.StructuredType structuredType,
            ReferenceDescriptionCollection typeDictionary,
            NamespaceTable namespaceTable)
        {
            var structureDefinition = new StructureDefinition()
            {
                BaseDataType = null,
                DefaultEncodingId = structuredType.QName.ToNodeId(typeDictionary, namespaceTable),
                Fields = new StructureFieldCollection(),
                StructureType = StructureType.Structure
            };

            bool isOptionalType = false;
            bool isSupportedType = true;
            bool isUnionOrOptionalType = false;
            bool hasBitField = false;
            bool isUnionType = false;

            foreach (var field in structuredType.Field)
            {
                // check for yet unsupported properties
                if (field.IsLengthInBytes ||
                    field.Terminator != null ||
                    field.Length != 0 ||
                    field.IsLengthInBytes)
                {
                    isSupportedType = false;
                }

                if (field.SwitchField != null)
                {
                    isUnionOrOptionalType = true;
                }

                if (field.SwitchValue != 0)
                {
                    isUnionType = true;
                }

                if (field.TypeName.Namespace == Namespaces.OpcBinarySchema ||
                    field.TypeName.Namespace == Namespaces.OpcUa)
                {
                    if (field.TypeName.Name == "Bit")
                    {
                        hasBitField = true;
                    }
                }
            }

            // test forbidden combinations
            if (!isSupportedType ||
                (isUnionType && hasBitField))
            {
                return null;
            }

            byte switchFieldBitPosition = 0;
            Int32 dataTypeFieldPosition = 0;
            var switchFieldBits = new Dictionary<string, byte>();
            // convert fields
            foreach (var field in structuredType.Field)
            {
                // consume optional bits
                if (field.TypeName.IsXmlBitType())
                {
                    var count = structureDefinition.Fields.Count;
                    if (count == 0 &&
                        switchFieldBitPosition < 32)
                    {
                        structureDefinition.StructureType = StructureType.StructureWithOptionalFields;
                        byte fieldLength = (byte)((field.Length == 0) ? 1u : field.Length);
                        switchFieldBits[field.Name] = switchFieldBitPosition;
                        switchFieldBitPosition += fieldLength;
                    }
                    else
                    {
                        // only support bit selectors at first
                        return null;
                    }
                    continue;
                }

                if (switchFieldBitPosition != 0 &&
                    switchFieldBitPosition != 32)
                {
                    return null;
                }

                var dataTypeField = new StructureField()
                {
                    Name = field.Name,
                    Description = null,
                    DataType = field.TypeName.ToNodeId(typeDictionary, namespaceTable),
                    IsOptional = false,
                    MaxStringLength = 0,
                    ArrayDimensions = null,
                    ValueRank = -1
                };

                // special case array
                if (field.LengthField != null)
                {
                    var lastField = structureDefinition.Fields.Last();
                    if (lastField.Name != field.LengthField)
                    {
                        // for arrays the length field must be 
                        // just before the type fields
                        return null;
                    }
                    lastField.Name = field.Name;
                    lastField.DataType = field.TypeName.ToNodeId(typeDictionary, namespaceTable);
                    lastField.ValueRank = 1;
                }
                else
                {
                    if (isUnionType)
                    {
                        // ignore the switchfield
                        if (field.SwitchField == null)
                        {
                            if (structureDefinition.Fields.Count != 0)
                            {
                                return null;
                            }
                            continue;
                        }
                        if (structureDefinition.Fields.Count != dataTypeFieldPosition)
                        {
                            return null;
                        }
                        dataTypeFieldPosition++;
                    }
                    else
                    {
                        if (field.SwitchField != null)
                        {
                            dataTypeField.IsOptional = true;
                            byte value;
                            if (!switchFieldBits.TryGetValue(field.SwitchField, out value))
                            {
                                return null;
                            }
                        }
                    }
                    structureDefinition.Fields.Add(dataTypeField);
                }
            }

            return structureDefinition;
        }

        public static bool IsXmlBitType(this XmlQualifiedName typeName)
        {
            if (typeName.Namespace == Namespaces.OpcBinarySchema ||
                typeName.Namespace == Namespaces.OpcUa)
            {
                if (typeName.Name == "Bit")
                {
                    return true;
                }
            }
            return false;
        }

        public static NodeId ToNodeId(
            this XmlQualifiedName typeName,
            ReferenceDescriptionCollection typeCollection,
            NamespaceTable namespaceTable)
        {
            if (typeName.Namespace == Namespaces.OpcBinarySchema ||
                typeName.Namespace == Namespaces.OpcUa)
            {
                // check for built in type
                if (typeName.Name == "CharArray")
                {
                    typeName = new System.Xml.XmlQualifiedName("String", typeName.Namespace);
                }
                var internalField = typeof(DataTypeIds).GetField(typeName.Name);
                return (NodeId)internalField.GetValue(typeName.Name);
            }
            else
            {
                var referenceId = typeCollection.Where(t =>
                    t.DisplayName == typeName.Name &&
                    t.NodeId.NamespaceUri == typeName.Namespace).FirstOrDefault();
                if (referenceId == null)
                {
                    // hackhack: servers may have multiple dictionaries with different
                    // target namespace but types are still in the same
                    referenceId = typeCollection.Where(t =>
                        t.DisplayName == typeName.Name).FirstOrDefault();
                }
                return ExpandedNodeId.ToNodeId(referenceId.NodeId, namespaceTable);
            }
        }
    }
}//namespace
