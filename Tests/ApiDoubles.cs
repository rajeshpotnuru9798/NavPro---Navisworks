// ONLY in the test executable. These doubles test our adapter's calls, not Autodesk runtime behavior.
using System;
using System.Collections.Generic;
namespace Autodesk.Navisworks.Api
{
    public enum VariantDataType { None, DisplayString, IdentifierString, Int32, Double, Boolean, DateTime, NamedConstant, Point3D }
    public sealed class NamedConstant { public string Name; public string DisplayName; }
    public sealed class VariantData
    {
        public VariantDataType DataType { get; }
        private readonly object value;
        public VariantData(object value, VariantDataType type) { this.value = value; DataType = type; }
        public VariantData(string value) { this.value = value; DataType = VariantDataType.DisplayString; }
        public bool IsNone => DataType == VariantDataType.None;
        public bool IsDisplayString => DataType == VariantDataType.DisplayString;
        public bool IsIdentifierString => DataType == VariantDataType.IdentifierString;
        public bool IsInt32 => DataType == VariantDataType.Int32;
        public bool IsBoolean => DataType == VariantDataType.Boolean;
        public bool IsDateTime => DataType == VariantDataType.DateTime;
        public bool IsAnyDouble => DataType == VariantDataType.Double;
        public bool IsNamedConstant => DataType == VariantDataType.NamedConstant;
        public string ToDisplayString() => (string)value;
        public string ToIdentifierString() => (string)value;
        public int ToInt32() => (int)value;
        public bool ToBoolean() => (bool)value;
        public DateTime ToDateTime() => (DateTime)value;
        public double ToAnyDouble() => (double)value;
        public NamedConstant ToNamedConstant() => (NamedConstant)value;
    }
    public sealed class Selection { public bool All; public void SelectAll() { All = true; } }
    public sealed class Search
    {
        public bool PruneBelowMatch { get; set; } = true;
        public Selection Selection { get; } = new Selection();
        public List<SearchCondition> SearchConditions { get; } = new List<SearchCondition>();
    }
    public sealed class SearchCondition
    {
        public string Category, Property;
        public VariantData EqualTo;
        public string Wildcard;
        public static SearchCondition HasPropertyByName(string category, string property) => new SearchCondition { Category = category, Property = property };
        public SearchCondition EqualValue(VariantData value) { EqualTo = value; return this; }
        public SearchCondition DisplayStringWildcard(string value) { Wildcard = value; return this; }
    }
}
