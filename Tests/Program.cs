using System;
using System.Linq;
using NavPro.Backend.Core;
using NavPro.Backend.Models;
using NavPro.Backend.Services;
using Autodesk.Navisworks.Api;
internal static class Program
{
    private static int count;
    private static void Assert(bool condition, string name) { if (!condition) throw new Exception(name); count++; Console.WriteLine("PASS " + name); }
    [STAThread]
    private static void Main()
    {
        NavPro.Frontend.Ui.WindowPreferences.StorageRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "NavPro-PolicyTests-" + Guid.NewGuid().ToString("N"));
        ToleranceTests.Run();
        var p = NamingPolicy.Plan(new[] { "001", "002" }, new string[0]);
        Assert(p[0].Name == "001" && p.All(v => v.Error == null), "Model numbers used directly as set names");
        Assert(NamingPolicy.Plan(new[] { "ABC", "abc" }, new string[0]).All(v => v.Error != null), "Batch duplicates case-insensitive");
        Assert(NamingPolicy.Plan(new[] { "ABC" }, new[] { "abc" })[0].Exists, "Existing names skipped case-insensitive");
        Assert(NamingPolicy.Plan(new[] { "  " }, new string[0])[0].Error != null, "Blank rejected");
        Assert(NamingPolicy.Plan(new[] { "A/B" }, new string[0])[0].Error != null, "Reserved character rejected");
        Assert(NamingPolicy.Plan(new[] { "ABC\n" }, new string[0])[0].Error != null, "Control character rejected");
        Assert(NamingPolicy.Plan(new[] { new string('X', 241) }, new string[0])[0].Error != null, "Long name rejected");
        Assert(NamingPolicy.Plan(new[] { "X " }, new string[0])[0].Error != null, "No silent trimming of model number");
        var modelNumberProperty = new PropertyOption { Key = new PropertyKey("Item", "ModelNumber"), Label = "Item / Model Number" };
        Assert(ReferenceEquals(ModelNumberPropertyPolicy.SelectUnique(new[] { modelNumberProperty }), modelNumberProperty), "Option B selects one explicit Model Number property");
        Assert(ModelNumberPropertyPolicy.Score("Item / Model No.") == 3, "Option B recognizes Model No label");
        Assert(ModelNumberPropertyPolicy.Score("Item / Model/File Number") == 3, "Option B recognizes Model/File Number label");
        Assert(ModelNumberPropertyPolicy.Score("Source / File Number") == 2, "Option B recognizes File Number fallback label");
        Assert(ModelNumberPropertyPolicy.Score("Item / Model") == 1, "Generic Model label is not explicit enough for Option B");
        Assert(ModelNumberPropertyPolicy.Score("Civil / Profile No") == 0, "Option B does not mistake Profile No for File No");
        var modelFileProperty = new PropertyOption { Key = new PropertyKey("Item", "ModelFileNumber"), Label = "Item / Model/File Number" };
        Assert(ReferenceEquals(ModelNumberPropertyPolicy.SelectUnique(new[] { modelFileProperty }), modelFileProperty), "Option B selects Model/File Number property");
        var fileNumberProperty = new PropertyOption { Key = new PropertyKey("Source", "FileNumber"), Label = "Source / File Number" };
        Assert(ReferenceEquals(ModelNumberPropertyPolicy.SelectUnique(new[] { modelNumberProperty, fileNumberProperty }), modelNumberProperty), "Explicit Model Number outranks File Number fallback");
        Assert(ReferenceEquals(ModelNumberPropertyPolicy.SelectUnique(new[] { fileNumberProperty }), fileNumberProperty), "Option B accepts one File Number fallback");
        bool ambiguous = false; try { ModelNumberPropertyPolicy.SelectUnique(new[] { modelNumberProperty, new PropertyOption { Key = new PropertyKey("User", "ModelNo"), Label = "User / Model No" } }); } catch (InvalidOperationException) { ambiguous = true; }
        Assert(ambiguous, "Option B rejects ambiguous model-number properties");
        bool notFound = false; try { ModelNumberPropertyPolicy.SelectUnique(new[] { new PropertyOption { Key = new PropertyKey("Item", "Model"), Label = "Item / Model" } }); } catch (InvalidOperationException) { notFound = true; }
        Assert(notFound, "Option B rejects missing explicit model-number properties");
        var key = new PropertyKey("InternalCategory", "InternalProperty");
        Assert(new CriteriaSpec(key, "DisplayString", "001").ValueKey != new CriteriaSpec(key, "Int32", "1").ValueKey, "Typed values do not collapse");
        Assert(new CriteriaSpec(key, "DisplayString", "ABC").ValueKey != new CriteriaSpec(key, "DisplayString", "abc").ValueKey, "Exact string values preserve case");
        Assert(new PropertyKey("a:b", "c").Id != new PropertyKey("a", "b:c").Id, "Internal property keys collision-safe");
        Assert(new CriteriaSpec(key, "DisplayString", "A").Property.Property == "InternalProperty", "Criteria uses internal property names");
        bool threw = false; try { new PropertyKey("", "p"); } catch (ArgumentException) { threw = true; }
        Assert(threw, "Missing property names rejected");
        var seenClashPairs = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        var parsedClashName = ClashTestNameParser.Parse("H_CI-Irrigation_VS_CS-Storm");
        Assert(parsedClashName.IsValid && parsedClashName.Priority == "H" && parsedClashName.DisciplineA == "CI-Irrigation" && parsedClashName.DisciplineB == "CS-Storm", "Clash test name extracts priority and full disciplines around VS");
        parsedClashName = ClashTestNameParser.Parse("M_SE-Security_VS_CW-Waste");
        Assert(parsedClashName.IsValid && parsedClashName.Priority == "M" && parsedClashName.DisciplineA == "SE-Security" && parsedClashName.DisciplineB == "CW-Waste", "Clash test parser preserves discipline descriptions and hyphens");
        parsedClashName = ClashTestNameParser.Parse("H_CI-Irrigation_CS-Storm");
        Assert(!parsedClashName.IsValid && parsedClashName.ValidationMessage.Contains("Expected Priority_DisciplineA_VS_DisciplineB"), "Invalid clash test names receive format validation");
        Assert(!ClashTestNameParser.Parse("H_CI-Irrigation_VS_CS-Storm_VS_Extra").IsValid, "Clash test parser rejects repeated VS separators");
        Assert(!ClashTestNameParser.Parse("H_VS_CS-Storm").IsValid, "VS separator cannot become an extracted discipline");
        Assert(ClashPairPolicy.TryClaimPair("RAD", "SAD", seenClashPairs), "First symmetric Clash Matrix pair is retained");
        Assert(!ClashPairPolicy.TryClaimPair("SAD", "RAD", seenClashPairs), "Reverse Clash Matrix pair is flagged as duplicate");
        Assert(ClashPairPolicy.EmptySourceValidation(0, true, 12, true).StartsWith("Warning:", StringComparison.Ordinal), "Empty Search Set pair remains creatable with warning");
        Assert(ClashPairPolicy.EmptySourceValidation(0, false, 12, true).StartsWith("Invalid:", StringComparison.Ordinal), "Empty explicit model selection remains blocked");
        var nativeText = new VariantData("001", VariantDataType.DisplayString);
        var textGroup = SearchCriteriaFactory.Read(key, nativeText);
        var search = SearchCriteriaFactory.Create(textGroup);
        Assert(search.Selection.All, "Adapter uses dynamic SelectAll");
        Assert(!search.PruneBelowMatch, "Adapter includes matching descendants");
        Assert(search.SearchConditions.Count == 1 && search.SearchConditions[0].Category == key.Category && search.SearchConditions[0].Property == key.Property, "Adapter uses one internal-name property condition");
        Assert(ReferenceEquals(search.SearchConditions[0].EqualTo, nativeText), "Adapter preserves original typed value in equality condition");
        var fullFilePath = new VariantData(@"C:\Users\someone\Models\EPD-JCB-MOD-CS-ZZ-0001.nwc", VariantDataType.DisplayString);
        var portableFileGroup = SearchCriteriaFactory.Read(new PropertyKey("Item", "FileName"), fullFilePath, true);
        var portableFileSearch = SearchCriteriaFactory.Create(portableFileGroup);
        Assert(portableFileGroup.Display == "EPD-JCB-MOD-CS-ZZ-0001.nwc", "File Name preview extracts basename from absolute path");
        Assert(portableFileSearch.SearchConditions[0].Wildcard == "*EPD-JCB-MOD-CS-ZZ-0001.nwc", "File Name condition uses path-independent filename suffix wildcard");
        Assert(portableFileSearch.SearchConditions[0].EqualTo == null, "Portable File Name condition does not compare against user-specific full path");
        var ordinaryPathValue = SearchCriteriaFactory.Read(key, fullFilePath, false);
        Assert(ordinaryPathValue.NativeValue.ToDisplayString() == fullFilePath.ToDisplayString(), "Other property values retain their original path");
        Assert(SearchCriteriaFactory.Read(key, new VariantData("  ", VariantDataType.DisplayString)) == null, "Adapter excludes whitespace values");
        Assert(SearchCriteriaFactory.Read(key, new VariantData(null, VariantDataType.None)) == null, "Adapter excludes missing values");
        Assert(SearchCriteriaFactory.Read(key, new VariantData("001", VariantDataType.IdentifierString)).Criteria.ValueKey != textGroup.Criteria.ValueKey, "Identifier and display strings remain distinct");
        Assert(SearchCriteriaFactory.Read(key, new VariantData(1, VariantDataType.Int32)).Display == "1", "Integer canonicalization");
        Assert(SearchCriteriaFactory.Read(key, new VariantData(1.25, VariantDataType.Double)).Display == "1.25", "Invariant numeric canonicalization");
        Assert(SearchCriteriaFactory.Read(key, new VariantData(new NamedConstant { Name = "stable-id", DisplayName = "Label" }, VariantDataType.NamedConstant)).Display == "Label", "Named constant keeps stable ID separate from label");
        threw = false; try { SearchCriteriaFactory.Read(key, new VariantData(null, VariantDataType.Point3D)); } catch (NotSupportedException) { threw = true; }
        Assert(threw, "Unsupported property type reported");
        Console.WriteLine(count + " policy/adapter tests passed. Adapter uses test doubles; no native Navisworks integration tested.");
    }
}
