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
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;

namespace Opc.Ua
{
    /// <summary>
    /// 
    /// </summary>
    public class ComplexTypeBuilder
    {
        #region Constructors
        /// <summary>
        /// Initializes the object with default values.
        /// </summary>
        public ComplexTypeBuilder(
            string targetNamespace,
            string assemblyName = null, 
            string moduleName = null)
        {
            m_targetNamespace = targetNamespace;
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(assemblyName ?? Guid.NewGuid().ToString()), AssemblyBuilderAccess.Run);
            var moduleBuilder = assemblyBuilder.GetDynamicModule(moduleName ?? m_opcTypesModuleName);
            if (moduleBuilder == null)
            {
                moduleBuilder = assemblyBuilder.DefineDynamicModule(moduleName ?? m_opcTypesModuleName);
            }
            m_moduleBuilder = moduleBuilder;
        }
        #endregion

        #region Public Properties
        public Type AddEnumType(Schema.Binary.EnumeratedType enumeratedType)
        {
            if (enumeratedType == null)
            {
                throw new ArgumentNullException(nameof(enumeratedType));
            }
            var enumBuilder = m_moduleBuilder.DefineEnum(enumeratedType.Name, TypeAttributes.Public, typeof(int));
            enumBuilder.SetCustomAttribute(DataContractAttributeBuilder(m_targetNamespace));
            foreach (var enumValue in enumeratedType.EnumeratedValue)
            {
                var newEnum = enumBuilder.DefineLiteral(enumValue.Name, enumValue.Value);
                newEnum.SetCustomAttribute(EnumAttributeBuilder(enumValue.Name, enumValue.Value));
            }
            return enumBuilder.CreateTypeInfo();
        }

        public ComplexTypeFieldBuilder AddStructuredType(string typeName)
        {
            var structureBuilder = m_moduleBuilder.DefineType(typeName, TypeAttributes.Public | TypeAttributes.Class, typeof(BaseComplexType));
            structureBuilder.SetCustomAttribute(DataContractAttributeBuilder(m_targetNamespace));
            return new ComplexTypeFieldBuilder(structureBuilder);
        }
        #endregion

        #region Static Members
        public static CustomAttributeBuilder DataContractAttributeBuilder(string Namespace)
        {
            var dataContractAttributeType = typeof(DataContractAttribute);
            ConstructorInfo ctorInfo = dataContractAttributeType.GetConstructor(Type.EmptyTypes);
            CustomAttributeBuilder enumBuilder = new CustomAttributeBuilder(
                ctorInfo,
                new object[0],  // constructor arguments
                new[]           // properties to assign
                {
                    dataContractAttributeType.GetProperty("Namespace")
                },
                new object[]    // values to assign
                {
                    Namespace
                });
            return enumBuilder;
        }

        public static CustomAttributeBuilder DataMemberAttributeBuilder(string name, bool isRequired, int order)
        {
            var dataMemberAttributeType = typeof(DataMemberAttribute);
            ConstructorInfo ctorInfo = dataMemberAttributeType.GetConstructor(Type.EmptyTypes);
            CustomAttributeBuilder enumBuilder = new CustomAttributeBuilder(
                ctorInfo,
                new object[0],  // constructor arguments
                new[]           // properties to assign
                {
                    dataMemberAttributeType.GetProperty("Name"),
                    dataMemberAttributeType.GetProperty("IsRequired"),
                    dataMemberAttributeType.GetProperty("Order")
                },
                new object[]    // values to assign
                {
                    name,
                    isRequired,
                    order
                });
            return enumBuilder;
        }

        public static CustomAttributeBuilder EnumAttributeBuilder(string Name, int Value)
        {
            var enumAttributeType = typeof(EnumMemberAttribute);
            Type[] ctorParams = new Type[] { typeof(string) };
            ConstructorInfo ctorInfo = enumAttributeType.GetConstructor(Type.EmptyTypes);
            CustomAttributeBuilder enumBuilder = new CustomAttributeBuilder(
                ctorInfo,
                new object[0],  // constructor arguments
                new[]           // properties to assign
                {
                    enumAttributeType.GetProperty("Value")
                },
                new object[]    // values to assign
                {
                    Name+"_"+Value.ToString()
                });
            return enumBuilder;
        }
        #endregion

        #region Private Members
        #endregion

        #region Private Fields
        private const string m_opcTypesModuleName = "Opc.Ua.ComplexType.Assembly";
        private ModuleBuilder m_moduleBuilder;
        private string m_targetNamespace;
        #endregion
    }

    /// <summary>
    /// Helper to build property fields.
    /// </summary>
    public class ComplexTypeFieldBuilder
    {
        #region Constructors
        public ComplexTypeFieldBuilder(TypeBuilder structureBuilder)
        {
            m_structureBuilder = structureBuilder;
        }
        #endregion

        #region Public Properties
        public void AddField(string fieldName, Type fieldType, int order)
        {
            var fieldBuilder = m_structureBuilder.DefineField("_" + fieldName, fieldType, FieldAttributes.Private);
            var propertyBuilder = m_structureBuilder.DefineProperty(fieldName, PropertyAttributes.None, fieldType, null);
            var methodAttributes =
                System.Reflection.MethodAttributes.Public |
                System.Reflection.MethodAttributes.HideBySig |
                System.Reflection.MethodAttributes.Virtual;

            var setBuilder = m_structureBuilder.DefineMethod("set_" + fieldName, methodAttributes, null, new[] { fieldType });
            var setIl = setBuilder.GetILGenerator();
            setIl.Emit(OpCodes.Ldarg_0);
            setIl.Emit(OpCodes.Ldarg_1);
            setIl.Emit(OpCodes.Stfld, fieldBuilder);
            setIl.Emit(OpCodes.Ret);

            var getBuilder = m_structureBuilder.DefineMethod("get_" + fieldName, methodAttributes, fieldType, Type.EmptyTypes);
            var getIl = getBuilder.GetILGenerator();
            getIl.Emit(OpCodes.Ldarg_0);
            getIl.Emit(OpCodes.Ldfld, fieldBuilder);
            getIl.Emit(OpCodes.Ret);

            propertyBuilder.SetGetMethod(getBuilder);
            propertyBuilder.SetSetMethod(setBuilder);
            propertyBuilder.SetCustomAttribute(ComplexTypeBuilder.DataMemberAttributeBuilder(fieldName, false, order));
        }

        public Type CreateType()
        {
            var complexType = m_structureBuilder.CreateType();
            m_structureBuilder = null;
            return complexType;
        }
        #endregion

        #region Private Fields
        private TypeBuilder m_structureBuilder;
        #endregion
    }
}//namespace
