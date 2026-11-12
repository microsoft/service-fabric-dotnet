// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using System.IO;
using Fuzzy;
using Microsoft.ServiceFabric.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.ServiceFabric.Client.Http.Serialization;

public abstract class RepairTaskConverterTest
{
    static readonly IFuzz fuzzy = new RandomFuzz(Environment.TickCount);

    public sealed class Deserialize : RepairTaskConverterTest
    {
        [Theory]
        [InlineData("None")]
        [InlineData("RestartNode")]
        [InlineData("RemoveNode")]
        [InlineData("RemoveNodeData")]
        [InlineData("FabricUpgrade")]
        [InlineData("ApplicationUpgrade")]
        [InlineData("Partition")]
        public void PreservesExternalPayloadThroughReadModifyWrite(string impactLevel)
        {
            JObject json = CreateTaskJson("External");
            json["Impact"] = new JObject
            {
                ["Kind"] = "External",
                ["ExternalImpactInfo"] = new JObject { ["ImpactLevel"] = impactLevel },
            };

            RepairTask task = Read(json);
            JObject written = Write(task);

            Assert.Equal((string)json["TaskId"], task.TaskId);
            Assert.Equal(RepairTargetKind.External, task.Target.Kind);
            Assert.Equal(RepairImpactKind.External, task.Impact.Kind);
            Assert.Equal(impactLevel, ((ExternalRepairImpactDescription)task.Impact).ImpactLevel.ToString());
            Assert.Equal(impactLevel, (string)written["Impact"]["ExternalImpactInfo"]["ImpactLevel"]);
            Assert.True(JToken.DeepEquals(json["Target"], written["Target"]));
            Assert.True(JToken.DeepEquals(json["Impact"], written["Impact"]));
            Assert.Equal((string)json["ExecutorData"], (string)written["ExecutorData"]);
            Assert.Equal("123", (string)written["Version"]);
        }

        [Fact]
        public void ReadsExternalTargetWithoutAnImpact()
        {
            RepairTask task = Read(CreateTaskJson("External"));
            JObject written = Write(task);
            Assert.Equal(RepairTargetKind.External, task.Target.Kind);
            Assert.Equal("External", (string)written["Target"]["Kind"]);
            Assert.Null(written["Impact"]);
        }

        [Fact]
        public void PreservesNodePayload()
        {
            JObject json = CreateTaskJson("Node");
            json["Target"]["NodeNames"] = new JArray(fuzzy.String());
            json["Impact"] = new JObject
            {
                ["Kind"] = "Node",
                ["NodeImpactList"] = new JArray(new JObject { ["NodeName"] = fuzzy.String(), ["ImpactLevel"] = "Restart" }),
            };

            RepairTask task = Read(json);
            JObject written = Write(task);

            Assert.Equal(RepairTargetKind.Node, task.Target.Kind);
            Assert.Equal(RepairImpactKind.Node, task.Impact.Kind);
            Assert.True(JToken.DeepEquals(json["Target"], written["Target"]));
            Assert.True(JToken.DeepEquals(json["Impact"], written["Impact"]));
        }

        [Theory]
        [InlineData("{}")]
        [InlineData("{\"ExternalImpactInfo\":null}")]
        [InlineData("{\"ExternalImpactInfo\":{}}")]
        [InlineData("{\"ExternalImpactInfo\":{\"ImpactLevel\":null}}")]
        [InlineData("{\"ExternalImpactInfo\":{\"ImpactLevel\":\"Unknown\"}}")]
        [InlineData("{\"ExternalImpactInfo\":{\"ImpactLevel\":99}}")]
        public void RejectsMalformedExternalImpact(string properties)
        {
            JObject json = CreateTaskJson("External");
            JObject impact = new() { ["Kind"] = "External" };
            impact.Merge(JObject.Parse(properties));
            json["Impact"] = impact;

            var exception = Assert.Throws<JsonReaderException>(() => Read(json));
            Assert.Contains("ExternalImpactInfo", exception.Message);
        }

        [Fact]
        public void SkipsUnknownExternalPropertiesWithoutLosingTheNextTaskField()
        {
            JObject json = CreateTaskJson("external");
            json["Target"]["FutureProperty"] = new JObject { ["Nested"] = new JArray(1, 2) };
            json["Impact"] = new JObject
            {
                ["Kind"] = "external",
                ["FutureProperty"] = true,
                ["externalimpactinfo"] = new JObject { ["FutureProperty"] = 1, ["impactlevel"] = "restartnode" },
            };
            string details = fuzzy.String();
            json["ResultDetails"] = details;

            RepairTask task = Read(json);

            Assert.Equal(RepairTargetKind.External, task.Target.Kind);
            Assert.Equal(ExternalRepairImpactLevel.RestartNode, ((ExternalRepairImpactDescription)task.Impact).ImpactLevel);
            Assert.Equal(details, task.ResultDetails);
        }
    }

    static JObject CreateTaskJson(string kind) =>
        new()
        {
            ["TaskId"] = fuzzy.String(),
            ["Version"] = "123",
            ["State"] = "Claimed",
            ["Action"] = fuzzy.String(),
            ["Target"] = new JObject { ["Kind"] = kind },
            ["Executor"] = fuzzy.String(),
            ["ExecutorData"] = fuzzy.String(),
        };

    static RepairTask Read(JObject json)
    {
        using JsonReader reader = json.CreateReader();
        return RepairTaskConverter.Deserialize(reader);
    }

    static JObject Write(RepairTask task)
    {
        using StringWriter text = new();
        using JsonWriter writer = new JsonTextWriter(text);
        RepairTaskConverter.Serialize(writer, task);
        return JObject.Parse(text.ToString());
    }
}
