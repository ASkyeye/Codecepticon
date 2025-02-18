﻿using Codecepticon.Utils;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Crypto.Prng;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities;
using Org.BouncyCastle.X509;
using System;
using System.Collections.Generic;
using System.IO;

namespace Codecepticon.Modules.Sign
{
    class CertificateManager
    {
        private const string SignatureAlgorithm = "SHA256WithRSA";

        private const int KeyLength = 2048;

        public bool GenerateCertificate(string Subject, string Issuer, DateTime NotBefore, DateTime NotAfter, string Password, string PfxOutput)
        {
            // https://mcse.cloud/create-a-self-signed-certificate-with-bouncy-castle-and-c/
            Logger.Debug("Initialising random generators and certificate generators...");
            SecureRandom secureRandom = new SecureRandom(new CryptoApiRandomGenerator());

            X509V3CertificateGenerator certificateGenerator = new X509V3CertificateGenerator();

            // Create and set serial number.
            Logger.Verbose("Setting up certificate properties...");
            BigInteger serialNumber = BigIntegers.CreateRandomInRange(BigInteger.One, BigInteger.ValueOf(Int64.MaxValue), secureRandom);
            certificateGenerator.SetSerialNumber(serialNumber);

            Logger.Debug("Creating Subject: " + Subject);
            X509Name subjectDN = new X509Name(true, Subject);
            certificateGenerator.SetSubjectDN(subjectDN);

            Logger.Debug("Creating Issuer: " + Issuer);
            X509Name issuerDN = new X509Name(true, Issuer);
            certificateGenerator.SetIssuerDN(issuerDN);

            Logger.Debug("Setting NotBefore: " + NotBefore);
            certificateGenerator.SetNotBefore(NotBefore);
            Logger.Debug("Setting NotAfter: " + NotAfter);
            certificateGenerator.SetNotAfter(NotAfter);

            KeyGenerationParameters keyGeneration = new KeyGenerationParameters(secureRandom, KeyLength);

            // Create RSA key.
            Logger.Verbose("Generating RSA keypair...");
            RsaKeyPairGenerator keyPairGenerator = new RsaKeyPairGenerator();
            keyPairGenerator.Init(keyGeneration);
            AsymmetricCipherKeyPair keyPair = keyPairGenerator.GenerateKeyPair();

            // Add the public/private keys to the certificate generator.
            Logger.Debug("Setting public key...");
            certificateGenerator.SetPublicKey(keyPair.Public);
            ISignatureFactory signatureFactory = new Asn1SignatureFactory(SignatureAlgorithm, keyPair.Private, secureRandom);
            X509Certificate certificate = certificateGenerator.Generate(signatureFactory);

            Logger.Debug("Creating keystore...");
            Pkcs12Store keyStore = new Pkcs12Store();
            X509CertificateEntry certificateEntry = new X509CertificateEntry(certificate);
            keyStore.SetCertificateEntry(certificate.SubjectDN.ToString(), certificateEntry);
            keyStore.SetKeyEntry(certificate.SubjectDN.ToString(), new AsymmetricKeyEntry(keyPair.Private), new[] { certificateEntry });

            // Convert to .NET Certificate.
            Logger.Debug("Converting to a .NET certificate...");
            MemoryStream stream = new MemoryStream();
            keyStore.Save(stream, Password.ToCharArray(), secureRandom);

            System.Security.Cryptography.X509Certificates.X509Certificate2 netCertificate = new System.Security.Cryptography.X509Certificates.X509Certificate2(stream.ToArray(), Password, System.Security.Cryptography.X509Certificates.X509KeyStorageFlags.PersistKeySet | System.Security.Cryptography.X509Certificates.X509KeyStorageFlags.Exportable);

            Logger.Verbose("Writing certificate to " + PfxOutput);
            File.WriteAllBytes(PfxOutput, netCertificate.Export(System.Security.Cryptography.X509Certificates.X509ContentType.Pfx, Password));
            return true;
        }

        public bool CheckPfxPassword(string pfxFile, string password)
        {
            try
            {
                Pkcs12Store keyStore = new Pkcs12Store(File.OpenRead(pfxFile), password.ToCharArray());
            } catch (Exception e)
            {
                return false;
            }
            
            return true;
        }

        public System.Security.Cryptography.X509Certificates.X509Certificate GetCertificateFromFile(string signedFile)
        {
            return System.Security.Cryptography.X509Certificates.X509Certificate2.CreateFromSignedFile(signedFile);
        }
    }
}
