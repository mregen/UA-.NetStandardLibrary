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
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;

namespace Opc.Ua.Client.ComplexTypes
{
    public static class ComplexTypesAttributeExtension
    {
        #region Static Members
        public static void DataContractAttribute(this TypeBuilder builder, string Namespace)
        {
            var attribute = DataContractAttributeBuilder(Namespace);
            builder.SetCustomAttribute(attribute);
        }

        public static void DataContractAttribute(this EnumBuilder builder, string Namespace)
        {
            var attribute = DataContractAttributeBuilder(Namespace);
            builder.SetCustomAttribute(attribute);
        }

        public static void DataMemberAttribute(this PropertyBuilder typeBuilder, string name, bool isRequired, int order)
        {
            var attribute = DataMemberAttributeBuilder(name, isRequired, order);
            typeBuilder.SetCustomAttribute(attribute);
        }

        public static void StructureDefinitonAttribute(
            this TypeBuilder typeBuilder, 
            StructureDefinition structureDefinition)
        {
            var attributeType = typeof(StructureDefinitionAttribute);
            var baseDataType = StructureDefinitionAttribute.FromBaseType(structureDefinition.BaseDataType);
            ConstructorInfo ctorInfo = attributeType.GetConstructor(Type.EmptyTypes);
            CustomAttributeBuilder builder = new CustomAttributeBuilder(
                ctorInfo,
                new object[0],  // constructor arguments
                new[]           // properties to assign
                {
                    attributeType.GetProperty("DefaultEncodingId"),
                    attributeType.GetProperty("BaseDataType"),
                    attributeType.GetProperty("StructureType")
                },
                new object[]    // values to assign
                {
                    structureDefinition.DefaultEncodingId.ToString(),
                    baseDataType,
                    structureDefinition.StructureType
                });
            typeBuilder.SetCustomAttribute(builder);
        }

        public static void StructureFieldAttribute(
            this PropertyBuilder typeBuilder, 
            StructureField structureField)
        {
            var attributeType = typeof(StructureFieldAttribute);
            ConstructorInfo ctorInfo = attributeType.GetConstructor(Type.EmptyTypes);
            CustomAttributeBuilder builder = new CustomAttributeBuilder(
                ctorInfo,
                new object[0],  // constructor arguments
                new[]           // properties to assign
                {
                    attributeType.GetProperty("ValueRank"),
                    attributeType.GetProperty("MaxStringLength"),
                    attributeType.GetProperty("IsOptional")
                },
                new object[]    // values to assign
                {
                    structureField.ValueRank,
                    structureField.MaxStringLength,
                    structureField.IsOptional
                });
            typeBuilder.SetCustomAttribute(builder);
        }

        public static void EnumAttribute(this FieldBuilder typeBuilder, string Name, int Value)
        {
            var attributeType = typeof(EnumMemberAttribute);
            Type[] ctorParams = new Type[] { typeof(string) };
            ConstructorInfo ctorInfo = attributeType.GetConstructor(Type.EmptyTypes);
            CustomAttributeBuilder builder = new CustomAttributeBuilder(
                ctorInfo,
                new object[0],  // constructor arguments
                new[]           // properties to assign
                {
                    attributeType.GetProperty("Value")
                },
                new object[]    // values to assign
                {
                    Name+"_"+Value.ToString()
                });
            typeBuilder.SetCustomAttribute(builder);
        }
        #endregion

        #region Private Static Members
        private static CustomAttributeBuilder DataMemberAttributeBuilder(string name, bool isRequired, int order)
        {
            var attributeType = typeof(DataMemberAttribute);
            ConstructorInfo ctorInfo = attributeType.GetConstructor(Type.EmptyTypes);
            CustomAttributeBuilder builder = new CustomAttributeBuilder(
                ctorInfo,
                new object[0],  // constructor arguments
                new[]           // properties to assign
                {
                    attributeType.GetProperty("Name"),
                    attributeType.GetProperty("IsRequired"),
                    attributeType.GetProperty("Order")
                },
                new object[]    // values to assign
                {
                    name,
                    isRequired,
                    order
                });
            return builder;
        }

        private static CustomAttributeBuilder DataContractAttributeBuilder(string Namespace)
        {
            var attributeType = typeof(DataContractAttribute);
            ConstructorInfo ctorInfo = attributeType.GetConstructor(Type.EmptyTypes);
            CustomAttributeBuilder builder = new CustomAttributeBuilder(
                ctorInfo,
                new object[0],  // constructor arguments
                new[]           // properties to assign
                {
                    attributeType.GetProperty("Namespace")
                },
                new object[]    // values to assign
                {
                    Namespace
                });
            return builder;
        }
        #endregion

    }
}//namespace
