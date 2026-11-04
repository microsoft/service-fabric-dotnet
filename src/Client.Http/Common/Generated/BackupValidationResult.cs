// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ServiceFabric.Common
{
    /// <summary>
    /// Defines values for BackupValidationResult.
    /// </summary>
    public enum BackupValidationResult
    {
        /// <summary>
        /// No validation was performed on this backup.
        /// </summary>
        None,

        /// <summary>
        /// All validation checks passed successfully.
        /// </summary>
        Success,

        /// <summary>
        /// Checksum validation failed because the calculated checksum of the entire backup does not match the expected
        /// checksum.
        /// </summary>
        ChecksumMismatchFailure,

        /// <summary>
        /// Backup chain validation failed because one or more backups in the chain are missing or corrupted.
        /// </summary>
        BackupChainMissingFailure,

        /// <summary>
        /// Backup integrity validation failed because the backup data is corrupted or inconsistent.
        /// </summary>
        IntegrityResultFailure,
    }
}
