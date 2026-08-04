using System.Reflection;
using NUnit.Framework;

namespace FragmentsUnity.Tests
{
    [TestFixture]
    public sealed class FragmentSceneLabelTests
    {
        private const string SanitizeLabelMethodName = "SanitizeLabel";
        private const string BuildElementLabelMethodName = "BuildElementLabel";

        private const string SafeLabel = "Wall-A_1";
        private const string PunctuatedLabel = "Basic Wall: Generic 200mm";
        private const string SanitizedPunctuatedLabel = "Basic_Wall__Generic_200mm";
        private const string NonAsciiLabel = "Wände 日";
        private const string SanitizedNonAsciiLabel = "W_nde__";

        private const int GeometryIndex = 0;
        private const int MaterialIndex = 0;
        private const int ElementLocalId = 7;
        private const string WallCategory = "IFCWALL";
        private const string CategoryLabel = "IFCWALL_7";
        private const string FallbackLabel = "Element_7";
        private const int NegativeLocalId = -1;
        private const string NegativeFallbackLabel = "Element_-1";

        [Test]
        public void SanitizeLabel_AlphanumericsHyphensAndUnderscores_SurviveUnchanged()
        {
            Assert.That(SanitizeLabel(SafeLabel), Is.EqualTo(SafeLabel));
        }

        [Test]
        public void SanitizeLabel_SpacesAndPunctuation_BecomeUnderscores()
        {
            Assert.That(SanitizeLabel(PunctuatedLabel), Is.EqualTo(SanitizedPunctuatedLabel));
        }

        [Test]
        public void SanitizeLabel_NonAsciiLetters_BecomeUnderscores()
        {
            Assert.That(SanitizeLabel(NonAsciiLabel), Is.EqualTo(SanitizedNonAsciiLabel));
        }

        [Test]
        public void SanitizeLabel_EmptyLabel_StaysEmpty()
        {
            Assert.That(SanitizeLabel(string.Empty), Is.Empty);
        }

        [Test]
        public void BuildElementLabel_NamedInstance_UsesTheSanitizedName()
        {
            FragmentInstance instance = FragmentSceneFixture.Instance(ElementLocalId, GeometryIndex, MaterialIndex);
            instance.Category = WallCategory;
            instance.Name = PunctuatedLabel;

            Assert.That(BuildElementLabel(instance), Is.EqualTo(SanitizedPunctuatedLabel));
        }

        [Test]
        public void BuildElementLabel_UnnamedInstance_UsesCategoryAndLocalId()
        {
            FragmentInstance instance = FragmentSceneFixture.Instance(ElementLocalId, GeometryIndex, MaterialIndex);
            instance.Category = WallCategory;

            Assert.That(BuildElementLabel(instance), Is.EqualTo(CategoryLabel));
        }

        [Test]
        public void BuildElementLabel_UnnamedAndUncategorizedInstance_UsesTheElementFallback()
        {
            FragmentInstance instance = FragmentSceneFixture.Instance(ElementLocalId, GeometryIndex, MaterialIndex);

            Assert.That(BuildElementLabel(instance), Is.EqualTo(FallbackLabel));
        }

        [Test]
        public void BuildElementLabel_NegativeLocalId_KeepsTheHyphenInTheFallback()
        {
            FragmentInstance instance = FragmentSceneFixture.Instance(NegativeLocalId, GeometryIndex, MaterialIndex);

            Assert.That(BuildElementLabel(instance), Is.EqualTo(NegativeFallbackLabel));
        }

        private static string SanitizeLabel(string label)
        {
            return (string)InternalMethod(SanitizeLabelMethodName).Invoke(null, new object[] { label });
        }

        private static string BuildElementLabel(FragmentInstance instance)
        {
            return (string)InternalMethod(BuildElementLabelMethodName).Invoke(null, new object[] { instance });
        }

        private static MethodInfo InternalMethod(string name)
        {
            MethodInfo method = typeof(FragmentSceneBuilder).GetMethod(
                name, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return method;
        }
    }
}
