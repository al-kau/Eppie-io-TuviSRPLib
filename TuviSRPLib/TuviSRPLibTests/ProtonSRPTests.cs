﻿using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities.Encoders;
using System.Text;
using TuviSRPLib;

namespace TuviSRPLibTests
{
    internal class ProtonSRPTests
    {
        [Test]
        public void FullCycleOfWorkTest()
        {
            var encodedN = "W2z5HBi8RvsfYzZTS7qBaUxxPhsfHJFZpu3Kd6s1JafNrCCH9rfvPLrfuqocxWPgWDH2R8neK7PkNvjxto9TStuY5z7jAzWRvFWN9cQhAKkdWgy0JY6ywVn22+HFpF4cYesHrqFIKUPDMSSIlWjBVmEJZ/MusD44ZT29xcPrOqeZvwtCffKtGAIjLYPZIEbZKnDM1Dm3q2K/xS5h+xdhjnndhsrkwm9U9oyA2wxzSXFL+pdfj2fOdRwuR5nW0J2NFrq3kJjkRmpO/Genq1UW+TEknIWAb6VzJJJA244K/H8cnSx2+nSNZO3bbo6Ys228ruV9A8m6DhxmS+bihN3ttQ==";
            var decodedN = Base64.Decode(encodedN);
            BigInteger N = new BigInteger(1, decodedN.Reverse().ToArray());
            BigInteger g = new BigInteger("2");

            string identity = "ivanov";
            string password = "qwerty";
            string salt = "some bytes";

            Encoding enc = Encoding.UTF8;
            byte[] identityBytes = enc.GetBytes(identity);
            byte[] passwordBytes = enc.GetBytes(password);
            byte[] saltBytes = enc.GetBytes(salt);

            ProtonSRPClient client = new ProtonSRPClient();
            ProtonSRPServer server = new ProtonSRPServer();
            IDigest digest = new ExtendedHashDigest();

            var verifier = ProtonSRPUtilities.CalculateVerifier(digest, N, g, saltBytes, identityBytes, passwordBytes);

            server.Init(N, g, verifier, digest, new SecureRandom());
            client.Init(N, g, digest, new SecureRandom());

            BigInteger pubA = client.GenerateClientCredentials(saltBytes, identityBytes, passwordBytes);
            BigInteger pubB = server.GenerateServerCredentials();

            BigInteger serverSecret = server.CalculateSecret(pubA);
            BigInteger clientSecret = client.CalculateSecret(pubB);

            BigInteger M1 = client.CalculateClientEvidenceMessage();
            Assert.IsTrue(server.VerifyClientEvidenceMessage(M1), "Message M1 is not verified.");

            BigInteger M2 = server.CalculateServerEvidenceMessage();

            Assert.IsTrue(client.VerifyServerEvidenceMessage(M2), "Message M2 is not verified.");

            BigInteger clientKey = client.CalculateSessionKey();
            BigInteger serverKey = server.CalculateSessionKey();

            Assert.AreEqual(clientKey, serverKey);
        }

        [Test]
        public void ClientSideWorkTest()
        {
            //pubB
            string testServerEphemeral = "l13IQSVFBEV0ZZREuRQ4ZgP6OpGiIfIjbSDYQG3Yp39FkT2B/k3n1ZhwqrAdy+qvPPFq/le0b7UDtayoX4aOTJihoRvifas8Hr3icd9nAHqd0TUBbkZkT6Iy6UpzmirCXQtEhvGQIdOLuwvy+vZWh24G2ahBM75dAqwkP961EJMh67/I5PA5hJdQZjdPT5luCyVa7BS1d9ZdmuR0/VCjUOdJbYjgtIH7BQoZs+KacjhUN8gybu+fsycvTK3eC+9mCN2Y6GdsuCMuR3pFB0RF9eKae7cA6RbJfF1bjm0nNfWLXzgKguKBOeF3GEAsnCgK68q82/pq9etiUDizUlUBcA==";
            var decodedPubB = Base64.Decode(testServerEphemeral);
            BigInteger pubB = new BigInteger(1, decodedPubB.Reverse().ToArray());

            //M2
            string testServerProof = "SLCSIClioSAtozauZZzcJuVPyY+MjnxfJSgEe9y6RafgjlPqnhQTZclRKPGsEhxVyWan7PIzhL+frPyZNaE1QaV5zbqz1yf9RXpGyTjZwU3FuVCJpkhp6iiCK3Wd2SemxawFXC06dgAdJ7I3HKvfkXeMANOUUh5ofjnJtXg42OGp4x1lKoFcH+IbB/CvRNQCmRTyhOiBJmZyUFwxHXLT/h+PlD0XSehcyybIIBIsscQ7ZPVPxQw4BqlqoYzTjjXPJxLxeQUQm2g9bPzT+izuR0VOPDtjt+dXrWny90k2nzS0Bs2YvNIqbJn1aQwFZr42p/O1I9n5S3mYtMgGk/7b1g==";
            var decodedExpectedM2 = Base64.Decode(testServerProof);
            BigInteger expectedM2 = new BigInteger(1, decodedExpectedM2.Reverse().ToArray());

            //M1
            string testClientProof = "Qb+1+jEqHRqpJ3nEJX2FEj0kXgCIWHngO0eT4R2Idkwke/ceCIUmQa0RfTYU53ybO1AVergtb7N0W/3bathdHT9FAHhy0vDGQDg/yPnuUneqV76NuU+pQHnO83gcjmZjDq/zvRRSD7dtIORRK97xhdR9W9bG5XRGr2c9Zev40YVcXgUiNUG/0zHSKQfEhUpMKxdauKtGC+dZnZzU6xaU0qvulYEsraawurRf0b1VXwohM6KE52Fj5xlS2FWZ3Mg0WIOC5KW5ziI6QirEUDK2pH/Rxvu4HcW9aMuppUmHk9Bm6kdg99o3vl0G7OgmEI7y6iyEYmXqH44XGORJ2sDMxQ==";
            var decodedExpectedM1 = Base64.Decode(testClientProof);
            BigInteger expectedM1 = new BigInteger(1, decodedExpectedM1.Reverse().ToArray());

            //N and g
            var encodedN = "W2z5HBi8RvsfYzZTS7qBaUxxPhsfHJFZpu3Kd6s1JafNrCCH9rfvPLrfuqocxWPgWDH2R8neK7PkNvjxto9TStuY5z7jAzWRvFWN9cQhAKkdWgy0JY6ywVn22+HFpF4cYesHrqFIKUPDMSSIlWjBVmEJZ/MusD44ZT29xcPrOqeZvwtCffKtGAIjLYPZIEbZKnDM1Dm3q2K/xS5h+xdhjnndhsrkwm9U9oyA2wxzSXFL+pdfj2fOdRwuR5nW0J2NFrq3kJjkRmpO/Genq1UW+TEknIWAb6VzJJJA244K/H8cnSx2+nSNZO3bbo6Ys228ruV9A8m6DhxmS+bihN3ttQ==";
            var decodedN = Base64.Decode(encodedN);
            BigInteger N = new BigInteger(1, decodedN.Reverse().ToArray());            
            BigInteger g = new BigInteger("2");

            //Digest
            IDigest digest = new ExtendedHashDigest();
            
            //Test data
            string identity = "jakubqa";
            string password = "abc123";
            string salt = "yKlc5/CvObfoiw==";
            string prA = "10547061652029274211379670715837497191923711392100181473801853905808809915196907607203711902581702530909229913139029064200053653545356956180378507124271109459013112604023928943361222711612802880534999338627841076012785708089125889096845658736374227261674415889530408226129007272971994571573711799978768722905740355338656395674139700290418014119543614116447579043620139396281282725306429481228395234306648949282792144922413465416627055298443842406176782173942480534905749407414063778620271297106813842950024831635672697955431839334459563366906834842208162136118219911675083220520501587197458892001573436641639539315377";
            BigInteger privA = new BigInteger(prA);
            
            Encoding enc = Encoding.UTF8;
            byte[] identityBytes = enc.GetBytes(identity);
            byte[] passwordBytes = enc.GetBytes(password);
            byte[] saltBytes = Base64.Decode(salt);

            //Client creation
            ProtonSRPClient client = new ProtonSRPClient();

            var pubA = client.InitAndGenerateCredential(N, g, digest, new SecureRandom(), privA, saltBytes, identityBytes, passwordBytes);
            BigInteger clientSecret = client.CalculateSecret(pubB);

            BigInteger M1 = client.CalculateClientEvidenceMessage();
            Assert.AreEqual(expectedM1, M1);

            Assert.IsTrue(client.VerifyServerEvidenceMessage(expectedM2), "Message M2 is not verified.");
        }
    }
}
