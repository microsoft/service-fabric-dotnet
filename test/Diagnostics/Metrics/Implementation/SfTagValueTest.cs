// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using Fuzzy;
using Xunit;

namespace Microsoft.ServiceFabric.Diagnostics.Metrics.Implementation;

public abstract class SfTagValueTest
{
    readonly SfTagValue sut;

    readonly string value = fuzzy.String();

    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    SfTagValueTest() => sut = new SfTagValue(value);

    public sealed class Equals_Object : SfTagValueTest
    {
        new readonly object sut;

        public Equals_Object() => sut = base.sut;

        [Fact]
        public void ReturnsTrueWhenObjIsSfTagValueOfEqualStringInstance() =>
            Assert.True(sut.Equals(new SfTagValue(new string(value.ToCharArray()))));

        [Fact]
        public void ReturnsFalseWhenObjIsSfTagValueOfDifferentString() =>
            Assert.False(sut.Equals(new SfTagValue(value + fuzzy.String())));

        // A wrapper and a string must never be the same tag value.
        [Fact]
        public void ReturnsFalseWhenObjIsTheString() =>
            Assert.False(sut.Equals(value));
    }

    public sealed class For : SfTagValueTest
    {
        [Fact]
        public void ReturnsSfTagValueOfValue() =>
            Assert.Same(value, ((SfTagValue)SfTagValue.For(fuzzy.String(), value)).ToString());

        [Fact]
        public void ReturnsNullWhenValueIsNull() =>
            Assert.Null(SfTagValue.For(fuzzy.String(), null));

        // The Geneva exporter reads routing values only from strings.
        [Theory]
        [InlineData("_microsoft_metrics_account")]
        [InlineData("_MICROSOFT_METRICS_NAMESPACE")]
        public void ReturnsValueOfGenevaRoutingKeyIgnoringCase(string key) =>
            Assert.Same(value, SfTagValue.For(key, value));
    }

    public new sealed class GetHashCode : SfTagValueTest
    {
        [Fact]
        public void ReturnsOrdinalHashCodeOfValue() =>
            Assert.Equal(StringComparer.Ordinal.GetHashCode(value), sut.GetHashCode());
    }

    public new sealed class ToString : SfTagValueTest
    {
        [Fact]
        public void ReturnsValue() =>
            Assert.Same(value, sut.ToString());
    }
}
