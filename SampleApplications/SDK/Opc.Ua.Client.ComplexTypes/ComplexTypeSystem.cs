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
using System.Threading.Tasks;

namespace Opc.Ua.Client.ComplexTypes
{
    /// <summary>
    /// 
    /// </summary>
    public class ComplexTypeSystem
    {
        #region Constructors
        /// <summary>
        /// Initializes the object with default values.
        /// </summary>
        public ComplexTypeSystem(Session session)
        {
            m_session = session;
        }
        #endregion

        #region Public Properties
        /// <summary>
        /// Load a single custom type with subtypes.
        /// </summary>
        public void LoadType(NodeId nodeId, bool subTypes)
        {
        }

        /// <summary>
        /// Load all custom types from a dictionary.
        /// </summary>
        public void LoadDictionary(NodeId nodeId)
        {
        }

        /// <summary>
        /// Load all custom types of a namespace.
        /// </summary>
        public void LoadDictionary(string nameSpace)
        {
        }

        /// <summary>
        /// Load all custom types from dictionaries into the sessions system type factory.
        /// </summary>
        public async Task Load()
        {
            var enumerationTypes = LoadDataTypes(DataTypeIds.Enumeration);

#if TEST
            var structuredTypes = LoadDataTypes(DataTypeIds.Structure);
            structuredTypes.AddRange(enumerationTypes);
            foreach (var structure in structuredTypes)
            {
                var result = m_session.ReadNode(ExpandedNodeId.ToNodeId(structure.NodeId, m_session.NamespaceUris)) as DataTypeNode;
                //if (result?.DataTypeDefinition != null)
                {
                    Console.WriteLine($"{structure}-{result?.DataTypeDefinition}");
                }
            }
#endif

            // load binary type system
            var typeSystem = await m_session.LoadDataTypeSystem();

            foreach (var dictionaryId in typeSystem)
            {
                var dictionary = dictionaryId.Value;
                // hackhack .. sort dictionary for dependencies
                var structureList = new List<Schema.Binary.TypeDescription>();
                var enumList = new List<Opc.Ua.Schema.Binary.TypeDescription>();
                var itemList = dictionary.TypeDictionary.Items.ToList();
                foreach (var item in itemList)
                {
                    var structuredObject = item as Opc.Ua.Schema.Binary.StructuredType;
                    if (structuredObject != null)
                    {
                        var dependentFields = structuredObject.Field.Where(f => f.TypeName.Namespace == dictionary.TypeDictionary.TargetNamespace);
                        if (dependentFields.Count() == 0)
                        {
                            structureList.Insert(0, structuredObject);
                        }
                        else
                        {
                            int insertIndex = 0;
                            foreach (var field in dependentFields)
                            {
                                int index = structureList.FindIndex(t => t.Name == field.Name);
                                if (index > insertIndex)
                                {
                                    insertIndex = index;
                                }
                            }
                            insertIndex++;
                            if (structureList.Count > insertIndex)
                            {
                                structureList.Insert(insertIndex, structuredObject);
                            }
                            else
                            {
                                structureList.Add(structuredObject);
                            }
                        }
                    }
                    else if (item is Opc.Ua.Schema.Binary.EnumeratedType)
                    {
                        enumList.Add(item);
                    }
                    else
                    {
                        throw new Exception();
                    }
                }

                var complexTypeBuilder = new ComplexTypeBuilder(dictionary.TypeDictionary.TargetNamespace);
                foreach (var enumType in enumerationTypes.Where(e => e.NodeId.NamespaceUri == dictionary.TypeDictionary.TargetNamespace))
                {
                    var nodeId = ExpandedNodeId.ToNodeId(enumType.NodeId, m_session.NamespaceUris);
                    var dataType = (DataTypeNode)m_session.ReadNode(nodeId);
                    if (dataType != null)
                    {
                        Type newType = null;
                        if (dataType.DataTypeDefinition != null)
                        {
                            // add enum type to module
                            newType = complexTypeBuilder.AddEnumType(enumType.BrowseName.Name, dataType.DataTypeDefinition);
                        }
                        else
                        {
                            // try dictionary enum definition
                            var enumeratedObject = enumList.Where(e => e.Name == enumType.BrowseName.Name).FirstOrDefault() as Opc.Ua.Schema.Binary.EnumeratedType;
                            if (enumeratedObject != null)
                            {
                                newType = complexTypeBuilder.AddEnumType(enumeratedObject);
                            }
                            else
                            {
                                // browse for EnumFields or EnumStrings property
                                var property = BrowseForSingleProperty(nodeId);
                                var enumArray = m_session.ReadValue(
                                    ExpandedNodeId.ToNodeId(property.NodeId,
                                    m_session.NamespaceUris));
                                if (enumArray.Value is ExtensionObject[])
                                {
                                    newType = complexTypeBuilder.AddEnumType(enumType.BrowseName.Name, (ExtensionObject[])enumArray.Value);
                                }
                                else if (enumArray.Value is LocalizedText[])
                                {
                                    newType = complexTypeBuilder.AddEnumType(enumType.BrowseName.Name, (LocalizedText[])enumArray.Value);
                                }
                            }
                        }
                        if (newType != null)
                        {
                            // match namespace and add to type factory
                            m_session.Factory.AddEncodeableType(enumType.NodeId, newType);
                        }
                    }
                }

#if Build_Enum_Types_from_Dictionary
                // build enums
                foreach (var item in enumList)
                {
                    var enumeratedObject = item as Opc.Ua.Schema.Binary.EnumeratedType;
                    if (enumeratedObject != null)
                    {
                        // add enum type to module
                        var newType = complexTypeBuilder.AddEnumType(enumeratedObject);
                        // match namespace and add to type factory
                        var referenceId = enumerationTypes.Where(t =>
                            t.DisplayName == enumeratedObject.Name &&
                            t.NodeId.NamespaceUri == dictionary.TypeDictionary.TargetNamespace).FirstOrDefault();
                        if (referenceId != null)
                        {
                            m_session.Factory.AddEncodeableType(referenceId.NodeId, newType);
                        }
                        else
                        {
                            Console.WriteLine($"ERROR: Failed to match enum type {enumeratedObject.Name} to namespace {dictionary.TypeDictionary.TargetNamespace}.");
                        }
                    }
                }
#endif

                // build classes
                foreach (var item in structureList)
                {
                    var structuredObject = item as Opc.Ua.Schema.Binary.StructuredType;
                    if (structuredObject != null)
                    {
                        bool missingTypeInfo = false;
                        var nodeId = dictionary.DataTypes.Where(d => d.Value.DisplayName == item.Name).FirstOrDefault().Value;

                        ExpandedNodeId typeId;
                        ExpandedNodeId binaryEncodingId;
                        DataTypeNode dataTypeNode;
                        bool newTypeDescription = BrowseTypeIdsForDictionaryComponent(
                            ExpandedNodeId.ToNodeId(nodeId.NodeId, m_session.NamespaceUris),
                            out typeId,
                            out binaryEncodingId,
                            out dataTypeNode);

                        var structureDefinition = dataTypeNode.DataTypeDefinition?.Body as StructureDefinition;
                        if (structureDefinition != null)
                        {
                            // use type definition (>= V1.04)
                            var structureBuilder = complexTypeBuilder.AddStructuredType(
                                dataTypeNode.BrowseName.Name,
                                structureDefinition
                                );

                            int order = 10;
                            foreach (var field in structureDefinition.Fields)
                            {
                                Type fieldType;
                                if (field.DataType.NamespaceIndex == 0)
                                {
                                    fieldType = Opc.Ua.TypeInfo.GetSystemType(field.DataType, m_session.Factory);
                                }
                                else
                                {
                                    fieldType = m_session.Factory.GetSystemType(NodeId.ToExpandedNodeId(field.DataType, m_session.NamespaceUris));
                                }

                                structureBuilder.AddField(field, fieldType, order);
                                order += 10;
                            }

                            var complexType = structureBuilder.CreateType();
                            m_session.Factory.AddEncodeableType(binaryEncodingId, complexType);
                            m_session.Factory.AddEncodeableType(typeId, complexType);
                        }
                        else
                        {
                            // use dictionary to build types (<=V1.03)
                            var structureBuilder = complexTypeBuilder.AddStructuredType(
                                structuredObject,
                                ExpandedNodeId.ToNodeId(typeId, m_session.NamespaceUris));

                            int order = 10;
                            bool unsupportedTypeInfo = false;
                            foreach (var field in structuredObject.Field)
                            {
                                // check for yet unsupported properties
                                if (field.IsLengthInBytes ||
                                    field.SwitchField != null ||
                                    field.Terminator != null ||
                                    field.LengthField != null ||
                                    field.Length != 0)
                                {
                                    unsupportedTypeInfo = true;
                                }

                                if (unsupportedTypeInfo)
                                {
                                    continue;
                                }

                                Type fieldType = null;
                                if (field.TypeName.Namespace == Namespaces.OpcBinarySchema ||
                                    field.TypeName.Namespace == Namespaces.OpcUa)
                                {
                                    if (field.TypeName.Name == "Bit")
                                    {
                                        unsupportedTypeInfo = true;
                                        continue;
                                    }
                                    // check for built in type
                                    if (field.TypeName.Name == "CharArray")
                                    {
                                        field.TypeName = new System.Xml.XmlQualifiedName("ByteString", field.TypeName.Namespace);
                                    }
                                    var internalField = typeof(DataTypeIds).GetField(field.TypeName.Name);
                                    var internalNodeId = (NodeId)internalField.GetValue(field.TypeName.Name);
                                    //var builtInType = Opc.Ua.TypeInfo.GetBuiltInType(internalNodeId);
                                    fieldType = Opc.Ua.TypeInfo.GetSystemType(internalNodeId, m_session.Factory);
                                }
                                else
                                {
                                    var referenceId = enumerationTypes.Where(t =>
                                        t.DisplayName == field.TypeName.Name &&
                                        t.NodeId.NamespaceUri == field.TypeName.Namespace).FirstOrDefault();
                                    if (referenceId != null)
                                    {
                                        fieldType = m_session.Factory.GetSystemType(referenceId.NodeId);
                                    }
                                    else
                                    {
                                        // TODO: find types from other dictionaries
                                    }
                                }
                                if (fieldType == null)
                                {
                                    // skip structured type ... missing datatype
                                    missingTypeInfo = true;
                                    continue;
                                }
                                Console.WriteLine($"Add: {field.Name}:{fieldType}-{order}");
                                structureBuilder.AddField(field.Name, fieldType, order);
                                order += 10;
                            }
                            if (!newTypeDescription)
                            {
                                Console.WriteLine($"--------- {item.Name}-Type id not found ------");
                            }

                            if (!missingTypeInfo && !unsupportedTypeInfo)
                            {
                                var complexType = structureBuilder.CreateType();
                                m_session.Factory.AddEncodeableType(binaryEncodingId, complexType);
                                m_session.Factory.AddEncodeableType(typeId, complexType);
                            }
                        }
                    }
                }
            }
        }
        #endregion

        #region Static Members
        #endregion

        #region Private Members
        /// <summary>
        /// Ensure the expanded nodeId contains a valid namespaceUri.
        /// </summary>
        /// <param name="expandedNodeId">The expanded nodeId.</param>
        /// <param name="namespaceTable">The session namespace table.</param>
        /// <returns>The normalized expanded nodeId.</returns>
        private ExpandedNodeId NormalizeExpandedNodeId(ExpandedNodeId expandedNodeId, NamespaceTable namespaceTable)
        {
            var nodeId = ExpandedNodeId.ToNodeId(expandedNodeId, namespaceTable);
            return NodeId.ToExpandedNodeId(nodeId, namespaceTable);
        }

        /// <summary>
        /// Browse for the type and encoding id for a dictionary component.
        /// </summary>
        /// <remarks>
        /// To find the typeId and encodingId for a dictionary type definition:
        /// i) inverse browse the description to get the encodingid
        /// ii) from the description inverse browse for encoding 
        /// to get the subtype typeid 
        /// </remarks>
        /// <param name="nodeId"></param>
        /// <param name="typeId"></param>
        /// <param name="encodingId"></param>
        /// <returns></returns>
        private bool BrowseTypeIdsForDictionaryComponent(
            NodeId nodeId,
            out ExpandedNodeId typeId,
            out ExpandedNodeId encodingId,
            out DataTypeNode dataTypeNode)
        {
            typeId = ExpandedNodeId.Null;
            encodingId = ExpandedNodeId.Null;
            dataTypeNode = null;

            Browser browser = new Browser(m_session);

            browser.BrowseDirection = BrowseDirection.Inverse;
            browser.ReferenceTypeId = ReferenceTypeIds.HasDescription;
            browser.IncludeSubtypes = false;
            browser.NodeClassMask = (int)NodeClass.Object;

            var references = browser.Browse(nodeId);

            if (references.Count == 1)
            {
                encodingId = references.First().NodeId;
                var encodingNodeId = ExpandedNodeId.ToNodeId(encodingId, m_session.NamespaceUris);
                encodingId = NodeId.ToExpandedNodeId(encodingNodeId, m_session.NamespaceUris);
                browser.BrowseDirection = BrowseDirection.Inverse;
                browser.ReferenceTypeId = ReferenceTypeIds.HasEncoding;
                browser.IncludeSubtypes = false;
                browser.NodeClassMask = (int)NodeClass.DataType;
                references = browser.Browse(encodingNodeId);
                if (references.Count == 1)
                {
                    typeId = NormalizeExpandedNodeId(references.First().NodeId, m_session.NamespaceUris);

                    var typeNodeId = ExpandedNodeId.ToNodeId(typeId, m_session.NamespaceUris);
                    dataTypeNode = m_session.ReadNode(typeNodeId) as DataTypeNode;

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Browse for the property.
        /// </summary>
        /// <remarks>
        /// Browse for property (type description) of an enum datatype.
        /// </remarks>
        /// <param name="nodeId"></param>
        /// <returns></returns>
        private ReferenceDescription BrowseForSingleProperty(
            NodeId nodeId)
        {
            Browser browser = new Browser(m_session);

            browser.BrowseDirection = BrowseDirection.Forward;
            browser.ReferenceTypeId = ReferenceTypeIds.HasProperty;
            browser.IncludeSubtypes = false;
            browser.NodeClassMask = (int)0;

            var references = browser.Browse(nodeId);

            if (references.Count == 1)
            {
                return references[0];
            }

            return null;
        }

        private ReferenceDescriptionCollection LoadDataTypes(NodeId dataType)
        {
            var result = new ReferenceDescriptionCollection();
            ReferenceDescriptionCollection references;
            Byte[] continuationPoint;

            var response = m_session.Browse(
                    null,
                    null,
                    dataType,
                    10u,
                    BrowseDirection.Forward,
                    ReferenceTypeIds.HasSubtype,
                    true,
                    (uint)NodeClass.DataType,
                    out continuationPoint,
                    out references);

            // filter out default namespace
            result.AddRange(references.Where(rd => rd.NodeId.NamespaceIndex != 0));

            while (continuationPoint != null)
            {
                Byte[] revisedContinuationPoint;
                response = m_session.BrowseNext(
                    null,
                    false,
                    continuationPoint,
                    out revisedContinuationPoint,
                    out references);
                result.AddRange(references.Where(rd => rd.NodeId.NamespaceIndex != 0));
                continuationPoint = revisedContinuationPoint;
            }

            NormalizeNodeIds(result);

            return result;
        }

        private void NormalizeNodeIds(ReferenceDescriptionCollection refCollection)
        {
            foreach (var reference in refCollection)
            {
                // fix expanded nodeids
                reference.NodeId = NormalizeExpandedNodeId(reference.NodeId, m_session.NamespaceUris);
            }
        }
        #endregion

        #region Private Fields
        Session m_session;
        #endregion
    }

}//namespace
