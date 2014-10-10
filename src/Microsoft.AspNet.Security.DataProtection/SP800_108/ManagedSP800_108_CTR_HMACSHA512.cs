﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography;
using Microsoft.AspNet.Security.DataProtection.Managed;

namespace Microsoft.AspNet.Security.DataProtection.SP800_108
{
    internal static class ManagedSP800_108_CTR_HMACSHA512
    {
        public static void DeriveKeys(byte[] kdk, ArraySegment<byte> label, ArraySegment<byte> context, Func<byte[], HashAlgorithm> prfFactory, ArraySegment<byte> output)
        {
            // make copies so we can mutate these local vars
            int outputOffset = output.Offset;
            int outputCount = output.Count;

            using (HashAlgorithm prf = prfFactory(kdk))
            {
                // See SP800-108, Sec. 5.1 for the format of the input to the PRF routine.
                byte[] prfInput = new byte[checked(sizeof(uint) /* [i]_2 */ + label.Count + 1 /* 0x00 */ + context.Count + sizeof(uint) /* [K]_2 */)];

                // Copy [L]_2 to prfInput since it's stable over all iterations
                uint outputSizeInBits = (uint)checked((int)outputCount * 8);
                prfInput[prfInput.Length - 4] = (byte)(outputSizeInBits >> 24);
                prfInput[prfInput.Length - 3] = (byte)(outputSizeInBits >> 16);
                prfInput[prfInput.Length - 2] = (byte)(outputSizeInBits >> 8);
                prfInput[prfInput.Length - 1] = (byte)(outputSizeInBits);

                // Copy label and context to prfInput since they're stable over all iterations
                Buffer.BlockCopy(label.Array, label.Offset, prfInput, sizeof(uint), label.Count);
                Buffer.BlockCopy(context.Array, context.Offset, prfInput, sizeof(int) + label.Count + 1, context.Count);

                int prfOutputSizeInBytes = prf.GetDigestSizeInBytes();
                for (uint i = 1; outputCount > 0; i++)
                {
                    // Copy [i]_2 to prfInput since it mutates with each iteration
                    prfInput[0] = (byte)(i >> 24);
                    prfInput[1] = (byte)(i >> 16);
                    prfInput[2] = (byte)(i >> 8);
                    prfInput[3] = (byte)(i);

                    // Run the PRF and copy the results to the output buffer
                    byte[] prfOutput = prf.ComputeHash(prfInput);
                    CryptoUtil.Assert(prfOutputSizeInBytes == prfOutput.Length, "prfOutputSizeInBytes == prfOutput.Length");
                    int numBytesToCopyThisIteration = Math.Min(prfOutputSizeInBytes, outputCount);
                    Buffer.BlockCopy(prfOutput, 0, output.Array, outputOffset, numBytesToCopyThisIteration);
                    Array.Clear(prfOutput, 0, prfOutput.Length); // contains key material, so delete it

                    // adjust offsets
                    outputOffset += numBytesToCopyThisIteration;
                    outputCount -= numBytesToCopyThisIteration;
                }
            }
        }
    }
}
