// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.Client.Http.Serialization
{
    using System;
    using System.Collections.Generic;
    using Microsoft.ServiceFabric.Common;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Converter for <see cref="CodePackagePatchProgress" />.
    /// </summary>
    internal class CodePackagePatchProgressConverter
    {
        /// <summary>
        /// Gets the enum value by reading string value from reader.
        /// </summary>
        /// <param name="reader">The <see cref="T: Newtonsoft.Json.JsonReader" /> to read from, reader must be placed at first property.</param>
        /// <returns>The enum Value.</returns>
        public static CodePackagePatchProgress? Deserialize(JsonReader reader)
        {
            var value = reader.ReadValueAsString();
            var obj = default(CodePackagePatchProgress);

            if (string.Compare(value, "Unknown", StringComparison.OrdinalIgnoreCase) == 0)
            {
                obj = CodePackagePatchProgress.Unknown;
            }
            else if (string.Compare(value, "DownloadPending", StringComparison.OrdinalIgnoreCase) == 0)
            {
                obj = CodePackagePatchProgress.DownloadPending;
            }
            else if (string.Compare(value, "PreparingContent", StringComparison.OrdinalIgnoreCase) == 0)
            {
                obj = CodePackagePatchProgress.PreparingContent;
            }
            else if (string.Compare(value, "RestartPending", StringComparison.OrdinalIgnoreCase) == 0)
            {
                obj = CodePackagePatchProgress.RestartPending;
            }
            else if (string.Compare(value, "ReplacementRunning", StringComparison.OrdinalIgnoreCase) == 0)
            {
                obj = CodePackagePatchProgress.ReplacementRunning;
            }
            else if (string.Compare(value, "NoCurrentActivation", StringComparison.OrdinalIgnoreCase) == 0)
            {
                obj = CodePackagePatchProgress.NoCurrentActivation;
            }
            else if (string.Compare(value, "AnotherMappingSelected", StringComparison.OrdinalIgnoreCase) == 0)
            {
                obj = CodePackagePatchProgress.AnotherMappingSelected;
            }
            else if (string.Compare(value, "DifferentCodePackageVersion", StringComparison.OrdinalIgnoreCase) == 0)
            {
                obj = CodePackagePatchProgress.DifferentCodePackageVersion;
            }
            else if (string.Compare(value, "RemovalPending", StringComparison.OrdinalIgnoreCase) == 0)
            {
                obj = CodePackagePatchProgress.RemovalPending;
            }
            else if (string.Compare(value, "RemovalComplete", StringComparison.OrdinalIgnoreCase) == 0)
            {
                obj = CodePackagePatchProgress.RemovalComplete;
            }
            else if (string.Compare(value, "Blocked", StringComparison.OrdinalIgnoreCase) == 0)
            {
                obj = CodePackagePatchProgress.Blocked;
            }

            return obj;
        }

        /// <summary>
        /// Serializes the enum value.
        /// </summary>
        /// <param name="writer">The <see cref="T: Newtonsoft.Json.JsonWriter" /> to write to.</param>
        /// <param name="value">The object to serialize to JSON.</param>
        public static void Serialize(JsonWriter writer, CodePackagePatchProgress? value)
        {
            switch (value)
            {
                case CodePackagePatchProgress.Unknown:
                    writer.WriteStringValue("Unknown");
                    break;
                case CodePackagePatchProgress.DownloadPending:
                    writer.WriteStringValue("DownloadPending");
                    break;
                case CodePackagePatchProgress.PreparingContent:
                    writer.WriteStringValue("PreparingContent");
                    break;
                case CodePackagePatchProgress.RestartPending:
                    writer.WriteStringValue("RestartPending");
                    break;
                case CodePackagePatchProgress.ReplacementRunning:
                    writer.WriteStringValue("ReplacementRunning");
                    break;
                case CodePackagePatchProgress.NoCurrentActivation:
                    writer.WriteStringValue("NoCurrentActivation");
                    break;
                case CodePackagePatchProgress.AnotherMappingSelected:
                    writer.WriteStringValue("AnotherMappingSelected");
                    break;
                case CodePackagePatchProgress.DifferentCodePackageVersion:
                    writer.WriteStringValue("DifferentCodePackageVersion");
                    break;
                case CodePackagePatchProgress.RemovalPending:
                    writer.WriteStringValue("RemovalPending");
                    break;
                case CodePackagePatchProgress.RemovalComplete:
                    writer.WriteStringValue("RemovalComplete");
                    break;
                case CodePackagePatchProgress.Blocked:
                    writer.WriteStringValue("Blocked");
                    break;
                default:
                    throw new ArgumentException($"Invalid value {value.ToString()} for enum type CodePackagePatchProgress");
            }
        }
    }
}
