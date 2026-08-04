using System.Collections.Generic;
using NUnit.Framework;

namespace FragmentsUnity.Tests
{
    [TestFixture]
    public sealed class FragmentTupleTokenizerTests
    {
        private const string NameValueTypeTuple = "[\"Name\",\"Wall\",\"IFCLABEL\"]";
        private const string EscapeTuple =
            "[\"\\n\",\"\\t\",\"\\r\",\"\\b\",\"\\f\",\"\\u0041\",\"q\\\"q\",\"p\\\\p\"]";
        private const string SpacedNumberTuple = "[ 42 ,  3.5 ]";
        private const string NestedTuple = "[1,[2,3],{\"a\":1}]";
        private const string NullTuple = "[\"a\",null,\"b\"]";
        private const string NoOpeningBracketTuple = "\"Name\",\"Wall\"";
        private const string UnterminatedQuoteTuple = "[\"Wall";
        private const int ExpectedMaxTupleTokens = 1024 * 1024;

        [Test]
        public void SplitTuple_NameValueTypeTuple_YieldsThreeTokens()
        {
            var tokens = new List<string>();

            FragmentTupleTokenizer.SplitTuple(NameValueTypeTuple, tokens);

            Assert.That(tokens, Is.EqualTo(new[] { "Name", "Wall", "IFCLABEL" }));
        }

        [Test]
        public void SplitTuple_QuotedEscapes_DecodeToControlCharactersAndLiterals()
        {
            var tokens = new List<string>();

            FragmentTupleTokenizer.SplitTuple(EscapeTuple, tokens);

            Assert.That(tokens, Is.EqualTo(new[]
            {
                "\n", "\t", "\r", "\b", "\f", "A", "q\"q", "p\\p"
            }));
        }

        [Test]
        public void SplitTuple_UnquotedNumbersWithSurroundingSpace_AreTrimmed()
        {
            var tokens = new List<string>();

            FragmentTupleTokenizer.SplitTuple(SpacedNumberTuple, tokens);

            Assert.That(tokens, Is.EqualTo(new[] { "42", "3.5" }));
        }

        [Test]
        public void SplitTuple_NestedArrayAndObject_StayRawSingleTokens()
        {
            var tokens = new List<string>();

            FragmentTupleTokenizer.SplitTuple(NestedTuple, tokens);

            Assert.That(tokens, Is.EqualTo(new[] { "1", "[2,3]", "{\"a\":1}" }));
        }

        [Test]
        public void SplitTuple_UnquotedNull_BecomesEmptyToken()
        {
            var tokens = new List<string>();

            FragmentTupleTokenizer.SplitTuple(NullTuple, tokens);

            Assert.That(tokens, Is.EqualTo(new[] { "a", string.Empty, "b" }));
        }

        [Test]
        public void SplitTuple_MissingOpeningBracket_YieldsNoTokens()
        {
            var tokens = new List<string>();

            FragmentTupleTokenizer.SplitTuple(NoOpeningBracketTuple, tokens);

            Assert.That(tokens, Is.Empty);
        }

        [Test]
        public void SplitTuple_UnterminatedQuote_KeepsTokenToEndOfInput()
        {
            var tokens = new List<string>();

            FragmentTupleTokenizer.SplitTuple(UnterminatedQuoteTuple, tokens);

            Assert.That(tokens, Is.EqualTo(new[] { "Wall" }));
        }

        [Test]
        public void SplitTuple_ReusedList_DiscardsPreviousTokens()
        {
            var tokens = new List<string>();

            FragmentTupleTokenizer.SplitTuple(NameValueTypeTuple, tokens);
            FragmentTupleTokenizer.SplitTuple(UnterminatedQuoteTuple, tokens);

            Assert.That(tokens, Is.EqualTo(new[] { "Wall" }));
        }

        [Test]
        public void SplitTuple_OrdinaryTuple_StaysFarBelowTheTokenCap()
        {
            var tokens = new List<string>();

            FragmentTupleTokenizer.SplitTuple(NestedTuple, tokens);

            Assert.That(FragmentImportLimits.MaxTupleTokens, Is.EqualTo(ExpectedMaxTupleTokens));
            Assert.That(tokens.Count, Is.LessThan(FragmentImportLimits.MaxTupleTokens));
        }
    }
}
