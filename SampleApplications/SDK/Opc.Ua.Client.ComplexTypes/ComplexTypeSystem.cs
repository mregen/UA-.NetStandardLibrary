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

        #region Public Members
        /// <summary>
        /// Load a single custom type with subtypes.
        /// </summary>
        public void LoadType(NodeId nodeId, bool subTypes)
        {
        }

        /// <summary>
        /// Load all custom types from a dictionary.
        /// </summary>
        public void LoadTypeDictionary(NodeId nodeId)
        {
        }

        /// <summary>
        /// Load all custom types of a namespace.
        /// </summary>
        public void LoadTypeDictionary(string nameSpace)
        {
        }

        /// <summary>
        /// Load all custom types from dictionaries into the sessions system type factory.
        /// </summary>
        public async Task LoadDictionaryDataTypes(
            IList<INode> serverEnumTypes,
            IList<INode> serverStructTypes
            )
        {
            var allTypes = new List<INode>();
            allTypes.AddRange(serverEnumTypes);
            allTypes.AddRange(serverStructTypes);

            // strip known types
            serverEnumTypes = RemoveKnownTypes(serverEnumTypes);
            serverStructTypes = RemoveKnownTypes(serverStructTypes);

            // load binary type system
            var typeSystem = await m_session.LoadDataTypeSystem();

            foreach (var dictionaryId in typeSystem)
            {
                var dictionary = dictionaryId.Value;
                var structureList = new List<Schema.Binary.TypeDescription>();
                var enumList = new List<Opc.Ua.Schema.Binary.TypeDescription>();

                SplitAndSortDictionary(dictionary, structureList, enumList);

                var complexTypeBuilder = new ComplexTypeBuilder(
                    dictionary.TypeDictionary.TargetNamespace,
                    m_session.NamespaceUris.GetIndex(dictionary.TypeDictionary.TargetNamespace));

                AddEnumTypes(complexTypeBuilder, enumList, serverEnumTypes);

                // build structures
                foreach (var item in structureList)
                {
                    var structuredObject = item as Opc.Ua.Schema.Binary.StructuredType;
                    if (structuredObject != null)
                    {
                        var nodeId = dictionary.DataTypes.Where(d => d.Value.DisplayName == item.Name).FirstOrDefault().Value;

                        ExpandedNodeId typeId;
                        ExpandedNodeId binaryEncodingId;
                        DataTypeNode dataTypeNode;
                        bool newTypeDescription = BrowseTypeIdsForDictionaryComponent(
                            ExpandedNodeId.ToNodeId(nodeId.NodeId, m_session.NamespaceUris),
                            out typeId,
                            out binaryEncodingId,
                            out dataTypeNode);

                        if (GetSystemType(typeId) != null)
                        {
                            continue;
                        }

                        StructureDefinition structureDefinition = dataTypeNode.DataTypeDefinition?.Body as StructureDefinition;
                        if (structureDefinition == null)
                        {
                            try
                            {
                                structureDefinition = structuredObject.ToStructureDefinition(
                                    binaryEncodingId, 
                                    allTypes, 
                                    m_session.NamespaceUris);
                            }
                            catch (ServiceResultException sre)
                            {
                                Utils.Trace($"Skip the type definition of {item.Name}. Error: {sre.Message}");
                                continue;
                            }
                        }

                        Type complexType = null;
                        if (structureDefinition != null)
                        {
                            complexType = AddStructuredType(
                                complexTypeBuilder,
                                structureDefinition,
                                dataTypeNode.BrowseName.Name);
                            if (complexType != null)
                            {
                                AddEncodeableType(binaryEncodingId, complexType);
                                AddEncodeableType(typeId, complexType);
                            }
                        }

                        if (complexType == null)
                        {
                            Utils.Trace($"Warning: Skipped the type definition of {item.Name}.");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Load all custom types from dictionaries into the sessions system type factory.
        /// </summary>
        public void LoadBaseDataTypes(
            IList<INode> serverEnumTypes,
            IList<INode> serverStructTypes
            )
        {
            // strip known types
            serverEnumTypes = RemoveKnownTypes(serverEnumTypes);
            serverStructTypes = RemoveKnownTypes(serverStructTypes);

            // add new enum Types for all namespaces
            var enumTypesToDoList = new List<INode>();
            int namespaceCount = m_session.NamespaceUris.Count;
            var complexTypeBuilders = new ComplexTypeBuilder[namespaceCount];
            for (uint i = 0; i < namespaceCount; i++)
            {
                var enumTypes = serverEnumTypes.Where(node => node.NodeId.NamespaceIndex == i).ToList();
                if (enumTypes.Count != 0)
                {
                    if (complexTypeBuilders[i] == null)
                    {
                        string targetNamespace = m_session.NamespaceUris.GetString(i);
                        complexTypeBuilders[i] = new ComplexTypeBuilder(targetNamespace, (int)i);
                    }
                    foreach (var enumType in enumTypes)
                    {
                        var newType = AddEnumType(complexTypeBuilders[i], enumType as DataTypeNode);
                        if (newType != null)
                        {
                            // match namespace and add to type factory
                            AddEncodeableType(enumType.NodeId, newType);
                        }
                        else
                        {
                            enumTypesToDoList.Add(enumType);
                        }
                    }
                }
            }

            bool retryAddStructType;
            var structTypesToDoList = new List<INode>();
            var structTypesWorkList = serverStructTypes;
            do
            {
                retryAddStructType = false;
                for (uint i = 0; i < namespaceCount; i++)
                {
                    var structTypes = structTypesWorkList.Where(node => node.NodeId.NamespaceIndex == i).ToList();
                    if (structTypes.Count != 0)
                    {
                        if (complexTypeBuilders[i] == null)
                        {
                            string targetNamespace = m_session.NamespaceUris.GetString(i);
                            complexTypeBuilders[i] = new ComplexTypeBuilder(
                                targetNamespace,
                                m_session.NamespaceUris.GetIndex(targetNamespace));
                        }
                        foreach (INode structType in structTypes)
                        {
                            Type newType = null;
                            var dataTypeNode = structType as DataTypeNode;
                            var structureDefinition = dataTypeNode.DataTypeDefinition?.Body as StructureDefinition;
                            if (structureDefinition != null)
                            {
                                try
                                {
                                    newType = AddStructuredType(complexTypeBuilders[i], structureDefinition, dataTypeNode.BrowseName.Name);
                                }
                                catch
                                {
                                    // creating the new type failed, likely a missing dependency, retry later
                                    retryAddStructType = true;
                                }
                                if (newType != null)
                                {
                                    // match namespace and add new type to type factory
                                    AddEncodeableType(structureDefinition.DefaultEncodingId, newType);
                                    AddEncodeableType(structType.NodeId, newType);
                                }
                            }
                            if (newType == null)
                            {
                                structTypesToDoList.Add(structType);
                            }
                        }
                    }
                }
                // due to type dependencies, retry missing types until there is no more improvement
                if (retryAddStructType &&
                    structTypesWorkList.Count != structTypesToDoList.Count)
                {
                    structTypesWorkList = structTypesToDoList;
                    structTypesToDoList = new List<INode>();
                }
            } while (retryAddStructType);
        }

        /// <summary>
        /// Load all custom types from a server into the session system type factory.
        /// </summary>
        public async Task Load()
        {
            // load server Types
            var serverEnumTypes = LoadDataTypes(DataTypeIds.Enumeration);
            var serverStructTypes = LoadDataTypes(DataTypeIds.Structure, true);

            LoadBaseDataTypes(serverEnumTypes, serverStructTypes);
            await LoadDictionaryDataTypes(serverEnumTypes, serverStructTypes);
            return;
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
        private ExpandedNodeId NormalizeExpandedNodeId(ExpandedNodeId expandedNodeId)
        {
            var nodeId = ExpandedNodeId.ToNodeId(expandedNodeId, m_session.NamespaceUris);
            return NodeId.ToExpandedNodeId(nodeId, m_session.NamespaceUris);
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
                    typeId = NormalizeExpandedNodeId(references.First().NodeId);

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
        private INode BrowseForSingleProperty(
            ExpandedNodeId nodeId)
        {
            Browser browser = new Browser(m_session);
            var references = m_session.NodeCache.FindReferences(
                nodeId,
                ReferenceTypeIds.HasProperty,
                false,
                false
                );
            return references.FirstOrDefault();
        }

        private IList<INode> LoadDataTypes(ExpandedNodeId dataType, bool subTypes = false, bool filterUATypes = true)
        {
            var result = new List<INode>();
            var nodesToBrowse = new ExpandedNodeIdCollection();
            nodesToBrowse.Add(dataType);

            while (nodesToBrowse.Count > 0)
            {
                var nextNodesToBrowse = new ExpandedNodeIdCollection();
                foreach (var node in nodesToBrowse)
                {
                    var response = m_session.NodeCache.FindReferences(
                        node,
                        ReferenceTypeIds.HasSubtype,
                        false,
                        false);

                    if (subTypes)
                    {
                        nextNodesToBrowse.AddRange(response.Select(r => r.NodeId).ToList());
                    }
                    if (filterUATypes)
                    {
                        // filter out default namespace
                        result.AddRange(response.Where(rd => rd.NodeId.NamespaceIndex != 0));
                    }
                    else
                    {
                        result.AddRange(response);
                    }
                }
                nodesToBrowse = nextNodesToBrowse;
            }

            return result;
        }

        private IList<INode> RemoveKnownTypes(IList<INode> nodeList)
        {
            return nodeList.Where(
                node => GetSystemType(node.NodeId) == null).ToList();
        }

        private Type GetSystemType(ExpandedNodeId nodeId)
        {
            if (!nodeId.IsAbsolute)
            {
                nodeId = NormalizeExpandedNodeId(nodeId);
            }
            return m_session.Factory.GetSystemType(nodeId);
        }

        /// <summary>
        /// 
        /// </summary>
        private void AddEnumTypes(
            ComplexTypeBuilder complexTypeBuilder,
            IList<Opc.Ua.Schema.Binary.TypeDescription> enumList,
            IList<INode> enumerationTypes
            )
        {
            foreach (var item in enumList)
            {
                Type newType = null;
                DataTypeNode enumType = enumerationTypes.Where(node =>
                    node.BrowseName.Name == item.Name &&
                    (node.NodeId.NamespaceIndex == complexTypeBuilder.TargetNamespaceIndex ||
                    complexTypeBuilder.TargetNamespaceIndex == -1)).FirstOrDefault()
                    as DataTypeNode;
                if (enumType != null)
                {
                    // try dictionary enum definition
                    var enumeratedObject = item as Schema.Binary.EnumeratedType;
                    if (enumeratedObject != null)
                    {
                        // 1. use Dictionary entry
                        newType = complexTypeBuilder.AddEnumType(enumeratedObject);
                    }
                    if (newType == null)
                    {
                        var dataType = m_session.NodeCache.Find(enumType.NodeId) as DataTypeNode;
                        if (dataType != null)
                        {
                            if (dataType.DataTypeDefinition != null)
                            {
                                // 1. use DataTypeDefinition 
                                newType = complexTypeBuilder.AddEnumType(enumType.BrowseName.Name, dataType.DataTypeDefinition);
                            }
                            else
                            {
                                // browse for EnumFields or EnumStrings property
                                var property = BrowseForSingleProperty(enumType.NodeId);
                                var enumArray = m_session.ReadValue(
                                    ExpandedNodeId.ToNodeId(property.NodeId, m_session.NamespaceUris));
                                if (enumArray.Value is ExtensionObject[])
                                {
                                    // 3. use EnumValues
                                    newType = complexTypeBuilder.AddEnumType(enumType.BrowseName.Name, (ExtensionObject[])enumArray.Value);
                                }
                                else if (enumArray.Value is LocalizedText[])
                                {
                                    // 4. use EnumStrings
                                    newType = complexTypeBuilder.AddEnumType(enumType.BrowseName.Name, (LocalizedText[])enumArray.Value);
                                }
                            }
                        }
                    }
                    if (newType != null)
                    {
                        // match namespace and add to type factory
                        AddEncodeableType(enumType.NodeId, newType);
                    }
                }
            }
        }

        private void AddEncodeableType(ExpandedNodeId nodeId, Type type)
        {
            m_session.Factory.AddEncodeableType(NormalizeExpandedNodeId(nodeId), type);
        }

        /// <summary>
        /// 
        /// </summary>
        private Type AddEnumType(
            ComplexTypeBuilder complexTypeBuilder,
            DataTypeNode enumTypeNode
            )
        {
            Type newType = null;
            if (enumTypeNode != null)
            {
                string name = enumTypeNode.BrowseName.Name;
                if (enumTypeNode.DataTypeDefinition != null)
                {
                    // 1. use DataTypeDefinition 
                    newType = complexTypeBuilder.AddEnumType(name, enumTypeNode.DataTypeDefinition);
                }
                else
                {
                    // browse for EnumFields or EnumStrings property
                    var property = BrowseForSingleProperty(enumTypeNode.NodeId);
                    var enumArray = m_session.ReadValue(
                        ExpandedNodeId.ToNodeId(property.NodeId,
                        m_session.NamespaceUris));
                    if (enumArray.Value is ExtensionObject[])
                    {
                        // 3. use EnumValues
                        newType = complexTypeBuilder.AddEnumType(name, (ExtensionObject[])enumArray.Value);
                    }
                    else if (enumArray.Value is LocalizedText[])
                    {
                        // 4. use EnumStrings
                        newType = complexTypeBuilder.AddEnumType(name, (LocalizedText[])enumArray.Value);
                    }
                }
            }
            return newType;
        }

        private Type AddStructuredType(
            ComplexTypeBuilder complexTypeBuilder,
            StructureDefinition structureDefinition,
            string typeName)
        {
            // check all types
            var typeList = new List<Type>();
            foreach (StructureField field in structureDefinition.Fields)
            {
                var newType = GetFieldType(field);
                if (newType == null)
                {
                    // missing that type
                    return null;
                }
                typeList.Add(newType);
            }

            var structureBuilder = complexTypeBuilder.AddStructuredType(
                typeName,
                structureDefinition
                );

            int order = 10;
            var typeListEnumerator = typeList.GetEnumerator();
            foreach (StructureField field in structureDefinition.Fields)
            {
                typeListEnumerator.MoveNext();
                structureBuilder.AddField(field, typeListEnumerator.Current, order);
                order += 10;
            }

            return structureBuilder.CreateType();
        }

        private Type GetFieldType(StructureField field)
        {
            Type fieldType = null;
            Type collectionType = null;
            if (field.DataType.NamespaceIndex == 0)
            {
                fieldType = Opc.Ua.TypeInfo.GetSystemType(field.DataType, m_session.Factory);
                if (field.ValueRank >= 0)
                {
                    if (fieldType == typeof(Byte[]))
                    {
                        collectionType = typeof(ByteStringCollection);
                    }
                    else
                    {
                        var assemblyQualifiedName = typeof(StatusCode).Assembly;
                        String collectionClassName = "Opc.Ua." + fieldType.Name + "Collection, " + assemblyQualifiedName;
                        collectionType = Type.GetType(collectionClassName);
                    }
                }
            }
            else
            {
                fieldType = GetSystemType(field.DataType);
                if (fieldType == null)
                {
                    return null;
                }
                if (field.ValueRank >= 0)
                {
                    String collectionClassName = (fieldType.Namespace != null) ? fieldType.Namespace + "." : "";
                    collectionClassName += fieldType.Name + "Collection, " + fieldType.Assembly;
                    collectionType = Type.GetType(collectionClassName);
                }
            }

            if (field.ValueRank >= 0)
            {
                if (collectionType != null)
                {
                    fieldType = collectionType;
                }
                else
                {
                    fieldType = fieldType.MakeArrayType();
                }
            }

            return fieldType;
        }

        /// <summary>
        /// Split the dictionary types into a list of structures and enumerations.
        /// Sort the structures by dependencies, with structures with dependent
        /// types at the end of the list, so they can be added to the factory in order.
        /// </summary>
        private void SplitAndSortDictionary(
                DataDictionary dictionary,
                List<Schema.Binary.TypeDescription> structureList,
                List<Schema.Binary.TypeDescription> enumList
                )
        {
            foreach (var item in dictionary.TypeDictionary.Items)
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
                    throw new ServiceResultException(StatusCodes.BadUnexpectedError, "Unexpected Type in binary schema.");
                }
            }
        }
        #endregion

        #region Private Fields
        Session m_session;
        #endregion
    }

}//namespace
