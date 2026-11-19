using System;
using System.Globalization;
using Autodesk.Navisworks.Api;
using NavPro.Backend.Core;
using NavPro.Backend.Models;

namespace NavPro.Backend.Services
{
    public static class SearchCriteriaFactory
    {
        public static ValueGroup Read(PropertyKey property, VariantData value, bool useFileNameValue = false)
        {
            if (value == null || value.IsNone) return null;
            string canonical, display;
            VariantData criteriaValue = value;
            bool matchFilenameSuffix = false;
            var ci = CultureInfo.InvariantCulture;
            if (value.IsDisplayString)
            {
                canonical = display = value.ToDisplayString();
                if (useFileNameValue && HasPath(canonical))
                {
                    canonical = display = System.IO.Path.GetFileName(canonical.Replace('/', '\\'));
                    criteriaValue = new VariantData(canonical);
                    matchFilenameSuffix = true;
                }
            }
            else if (value.IsIdentifierString) canonical = display = value.ToIdentifierString();
            else if (value.IsInt32) canonical = display = value.ToInt32().ToString(ci);
            else if (value.IsBoolean) canonical = display = value.ToBoolean().ToString();
            else if (value.IsDateTime) canonical = display = value.ToDateTime().ToString("O", ci);
            else if (value.IsAnyDouble) canonical = display = value.ToAnyDouble().ToString("R", ci);
            else if (value.IsNamedConstant)
            {
                var named = value.ToNamedConstant(); canonical = named.Name; display = named.DisplayName;
            }
            else throw new NotSupportedException("Model numbers must be scalar properties (text, number, date, boolean or named constant).");
            if (string.IsNullOrWhiteSpace(canonical) || string.IsNullOrWhiteSpace(display)) return null;
            return new ValueGroup { Criteria = new CriteriaSpec(property, criteriaValue.DataType.ToString(), canonical), NativeValue = criteriaValue, Display = display, MatchFilenameSuffix = matchFilenameSuffix };
        }

        private static bool HasPath(string value) => value.IndexOf('/') >= 0 || value.IndexOf('\\') >= 0;
        public static Search Create(ValueGroup group)
        {
            var search = new Search { PruneBelowMatch = false };
            search.Selection.SelectAll(); // Dynamic all-model selection, never explicit item IDs.
            var key = group.Criteria.Property;
            var condition = SearchCondition.HasPropertyByName(key.Category, key.Property);
            search.SearchConditions.Add(group.MatchFilenameSuffix
                ? condition.DisplayStringWildcard("*" + group.NativeValue.ToDisplayString())
                : condition.EqualValue(group.NativeValue));
            return search;
        }
    }
}
