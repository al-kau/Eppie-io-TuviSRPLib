﻿using System;
using System.Linq;
using System.Text;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities;
using ProtonBase64Lib;

namespace TuviSRPLib
{
    public class ProtonSRPUtilities
    {
        private const string proton = "proton";
        private const int cost = 10;

        /// <summary>
        /// Calculates value of multiplier K.
        /// </summary>
        /// <param name="digest">Digest (hash algorithm).</param>
        /// <param name="g">Field generator.</param>
        /// <param name="N">Field order.</param>
        /// <returns>Multiplier value.</returns>
        public static BigInteger CalculateK(IDigest digest, BigInteger g, BigInteger N)
        {
            return HashPaddedPair(digest, N, g, N);
        }

        /// <summary>
        /// Calculates parameter U or ScrambleParam in Proton realization.
        /// </summary>
        /// <param name="digest">Digest (hash algorithm).</param>
        /// <param name="N">Field order.</param>
        /// <param name="A">Client's public value.</param>
        /// <param name="B">Server's public value.</param>
        /// <returns>U value.</returns>
        public static BigInteger CalculateU(IDigest digest, BigInteger N, BigInteger A, BigInteger B)
        {
            return HashPaddedPair(digest, N, A, B);
        }

        /// <summary>
        /// Calculates parameter X or HashedPassword in Proton realization.
        /// </summary>
        /// <param name="digest">Digest (hash algorithm).</param>
        /// <param name="N">Field order.</param>
        /// <param name="salt">Salt.</param>
        /// <param name="identity">Client's identity (User name).</param>
        /// <param name="password">Password.</param>
        /// <returns>X value.</returns>
        public static BigInteger CalculateX(IDigest digest, BigInteger N, byte[] salt, byte[] identity, byte[] password)
        {
            int paddedLength = (N.BitLength + 7) / 8;
            byte[] output = new byte[digest.GetDigestSize()];
            Encoding enc = Encoding.UTF8;
            byte[] byteProton = enc.GetBytes(proton);
            var extSalt = Append(salt, byteProton);

            var newPassword = Append(password, new byte[] { 0 });

            var hashedPassword = BCrypt.Generate(newPassword, extSalt, cost);

            var message = FormMessage(extSalt, hashedPassword);

            digest.BlockUpdate(message, 0, message.Length);
            byte[] bytes = N.ToLowEndianNByteArray(paddedLength);
            digest.BlockUpdate(bytes, 0, bytes.Length);

            digest.DoFinal(output, 0);

            return new BigInteger(1, output.Reverse().ToArray());
        }

        /// <summary>
        /// Forming special sequence of bytes used in proton protocol.
        /// </summary>
        /// <param name="extSalt">Extended salt (salt + "proton").</param>
        /// <param name="hashedPassword">Hashed password (bcrypt algorithm).</param>
        /// <returns>Byte sequence.</returns>
        private static byte[] FormMessage(byte[] extSalt, byte[] hashedPassword)
        {
            //TODO: create dynamic prefix according to proton realization
            byte[] prefix = new byte[] { 36, 50, 121, 36, 49, 48, 36 }; // "$2y$10$
            
            var addSalt = ProtonBase64.Encode(extSalt);

            var shortenedPassword = new byte[hashedPassword.Length - 1];
            Array.Copy(hashedPassword, 0, shortenedPassword, 0, shortenedPassword.Length);
            var addPass = ProtonBase64.Encode(shortenedPassword);

            return Append(Append(prefix, addSalt), addPass);
        }

        /// <summary>
        /// Calculates verifier V.
        /// </summary>
        /// <param name="digest">Digest (hash algorithm).</param>
        /// <param name="N">Field order.</param>
        /// <param name="g">Field generator.</param>
        /// <param name="salt">Salt.</param>
        /// <param name="identity">Client's identity (User name).</param>
        /// <param name="password">Password.</param>
        /// <returns>Verifier value.</returns>
        public static BigInteger CalculateVerifier(IDigest digest, BigInteger N, BigInteger g, byte[] salt, byte[] identity, byte[] password)
        {
            var x = CalculateX(digest, N, salt, identity, password);
            return g.ModPow(x, N);
        }

        public static BigInteger GeneratePrivateValue(IDigest digest, BigInteger N, BigInteger g, SecureRandom random)
        {
            int minBits = System.Math.Min(256, N.BitLength / 2);
            BigInteger min = BigInteger.One.ShiftLeft(minBits - 1);
            BigInteger max = N.Subtract(BigInteger.One);

            return BigIntegers.CreateRandomInRange(min, max, random);
        }

        public static BigInteger ValidatePublicValue(BigInteger N, BigInteger val)
        {
            val = val.Mod(N);

            // Check that val % N != 0
            if (val.Equals(BigInteger.Zero))
                throw new CryptoException("Invalid public value: 0");

            return val;
        }

        /** 
         * Computes the client evidence message (M1) according to the standard routine:
         * M1 = H( A | B | S )
         * @param digest The Digest used as the hashing function H
         * @param N Modulus used to get the pad length
         * @param A The public client value
         * @param B The public server value
         * @param S The secret calculated by both sides
         * @return M1 The calculated client evidence message
         */
        public static BigInteger CalculateM1(IDigest digest, BigInteger N, BigInteger A, BigInteger B, BigInteger S)
        {
            BigInteger M1 = HashPaddedTriplet(digest, N, A, B, S);
            return M1;
        }

        /** 
         * Computes the server evidence message (M2) according to the standard routine:
         * M2 = H( A | M1 | S )
         * @param digest The Digest used as the hashing function H
         * @param N Modulus used to get the pad length
         * @param A The public client value
         * @param M1 The client evidence message
         * @param S The secret calculated by both sides
         * @return M2 The calculated server evidence message
         */
        public static BigInteger CalculateM2(IDigest digest, BigInteger N, BigInteger A, BigInteger M1, BigInteger S)
        {
            BigInteger M2 = HashPaddedTriplet(digest, N, A, M1, S);
            return M2;
        }

        /**
         * Computes the final Key according to the standard routine: Key = H(S)
         * @param digest The Digest used as the hashing function H
         * @param N Modulus used to get the pad length
         * @param S The secret calculated by both sides
         * @return
         */
        public static BigInteger CalculateKey(IDigest digest, BigInteger N, BigInteger S)
        {
            int paddedLength = (N.BitLength + 7) / 8;
            int digestSize = digest.GetDigestSize();

            byte[] bytes = S.ToLowEndianNByteArray(paddedLength);
            digest.BlockUpdate(bytes, 0, bytes.Length);

            byte[] output = new byte[digestSize];
            digest.DoFinal(output, 0);

            return new BigInteger(1, output.Reverse().ToArray());
        }

        private static BigInteger HashPaddedTriplet(IDigest digest, BigInteger N, BigInteger n1, BigInteger n2, BigInteger n3)
        {
            int paddedLength = (N.BitLength + 7) / 8;
            int digestSize = digest.GetDigestSize();

            byte[] bytes = n1.ToLowEndianNByteArray(paddedLength);
            digest.BlockUpdate(bytes, 0, bytes.Length);
            bytes = n2.ToLowEndianNByteArray(paddedLength);
            digest.BlockUpdate(bytes, 0, bytes.Length);
            bytes = n3.ToLowEndianNByteArray(paddedLength);
            digest.BlockUpdate(bytes, 0, bytes.Length);

            byte[] output = new byte[digestSize];
            digest.DoFinal(output, 0);

            return new BigInteger(1, output.Reverse().ToArray());
        }

        private static BigInteger HashPaddedPair(IDigest digest, BigInteger N, BigInteger n1, BigInteger n2)
        {
            int paddedLength = (N.BitLength + 7) / 8;
            int digestSize = digest.GetDigestSize();

            byte[] bytes = n1.ToLowEndianNByteArray(paddedLength);
            digest.BlockUpdate(bytes, 0, bytes.Length);
            bytes = n2.ToLowEndianNByteArray(paddedLength);
            digest.BlockUpdate(bytes, 0, bytes.Length);

            byte[] output = new byte[digestSize];
            digest.DoFinal(output, 0);

            return new BigInteger(1, output.Reverse().ToArray());
        }

        private static byte[] Append(byte[] arr1, byte[] arr2)
        {
            return arr1.Concat(arr2).ToArray();
        }
    }
}
