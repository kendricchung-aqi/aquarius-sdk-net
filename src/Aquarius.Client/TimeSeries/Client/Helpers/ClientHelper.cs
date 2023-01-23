﻿using System;
using System.Security.Cryptography;
using System.Text;
#if NETSTANDARD
using System.Xml;
#endif
using Aquarius.Helpers;
using Aquarius.TimeSeries.Client.ServiceModels.Publish;
using ServiceStack;
using GetPublicKey = Aquarius.TimeSeries.Client.ServiceModels.Publish.GetPublicKey;

namespace Aquarius.TimeSeries.Client.Helpers
{
    public class ClientHelper
    {
        public static void SetAuthenticationToken(ServiceClientBase client, string authenticationToken)
        {
            client.Headers.Remove(AuthenticationHeaders.AuthenticationHeaderNameKey);
            client.Headers.Add(AuthenticationHeaders.AuthenticationHeaderNameKey, authenticationToken);

            ExpireExistingAuthenticationCookie(client);
        }

        private static void ExpireExistingAuthenticationCookie(ServiceClientBase client)
        {
            if (client.BaseUri == null) return;

            var existingAuthCookie = client.CookieContainer.GetCookies(new Uri(client.BaseUri))[AuthenticationHeaders.AuthenticationCookieName];

            if (existingAuthCookie == null) return;

            existingAuthCookie.Expired = true;
        }

        private static void ClearAuthenticationToken(ServiceClientBase client)
        {
            if (client == null) return;

            client.Headers.Remove(AuthenticationHeaders.AuthenticationHeaderNameKey);
            client.ClearCookies();
        }

        public static string Login(IServiceClient client, string username, string password, string aqiIdpAccessToken)
        {
            ClearAuthenticationToken(client as ServiceClientBase);

            if (!string.IsNullOrEmpty(aqiIdpAccessToken))
            {
                return LoginWithAqiIdpAccessToken(aqiIdpAccessToken);
            }

            var publicKey = client.Get(new GetPublicKey());
            var encryptedPassword = EncryptPassword(publicKey.Xml, password);
            return client.Post(new PostSession {EncryptedPassword = encryptedPassword, Username = username});
        }

        private static string LoginWithAqiIdpAccessToken(string aqiIdpAccessToken)
        {
            var baseUri = "http://ewqlovltkchung:1234/";
            var client = new SdkServiceClient(baseUri);

            return client.Post(new PostSessionToken { Token = aqiIdpAccessToken });
        }

        public static void Logout(IServiceClient client)
        {
            client.Delete(new DeleteSession());
        }

        private static string EncryptPassword(string publicKeyXml, string plaintextPassword)
        {
            using (var rsaCrypto = new RSACryptoServiceProvider())
            {
                LoadFromXmlString(rsaCrypto, publicKeyXml);

                var plaintextBytes = Encoding.UTF8.GetBytes(plaintextPassword);
                var encryptedBytes = rsaCrypto.Encrypt(plaintextBytes, true);
                var encryptedBase64 = Convert.ToBase64String(encryptedBytes);

                return encryptedBase64;
            }
        }

        private static void LoadFromXmlString(RSACryptoServiceProvider rsaCrypto, string publicKeyXml)
        {
#if NETFRAMEWORK || NET6_0_OR_GREATER
            rsaCrypto.FromXmlString(publicKeyXml);
#else
            // Thanks to: https://gist.github.com/Jargon64/5b172c452827e15b21882f1d76a94be4
            var parameters = new RSAParameters();

            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(publicKeyXml);

            // ReSharper disable once PossibleNullReferenceException
            if (xmlDoc.DocumentElement.Name.Equals("RSAKeyValue"))
            {
                foreach (XmlNode node in xmlDoc.DocumentElement.ChildNodes)
                {
                    switch (node.Name)
                    {
                        case "Modulus": parameters.Modulus = Convert.FromBase64String(node.InnerText); break;
                        case "Exponent": parameters.Exponent = Convert.FromBase64String(node.InnerText); break;
                        case "P": parameters.P = Convert.FromBase64String(node.InnerText); break;
                        case "Q": parameters.Q = Convert.FromBase64String(node.InnerText); break;
                        case "DP": parameters.DP = Convert.FromBase64String(node.InnerText); break;
                        case "DQ": parameters.DQ = Convert.FromBase64String(node.InnerText); break;
                        case "InverseQ": parameters.InverseQ = Convert.FromBase64String(node.InnerText); break;
                        case "D": parameters.D = Convert.FromBase64String(node.InnerText); break;
                    }
                }
            }
            else
            {
                throw new Exception("Invalid XML RSA key.");
            }

            rsaCrypto.ImportParameters(parameters);
#endif
        }

        public static JsonServiceClient CloneAuthenticatedClient(ServiceClientBase client, string baseUri)
        {
            if (client == null)
                throw new ArgumentNullException(nameof(client));

            var builder = new UriBuilder(client.BaseUri)
            {
                Path = baseUri
            };

            var clone = new SdkServiceClient(builder.ToString());

            foreach (var headerKey in client.Headers.AllKeys)
            {
                clone.Headers.Remove(headerKey);
                clone.Headers.Add(headerKey, client.Headers[headerKey]);
            }

            clone.Timeout = client.Timeout;
            clone.ReadWriteTimeout = client.ReadWriteTimeout;
            clone.OnAuthenticationRequired = client.OnAuthenticationRequired;
            clone.RequestFilter = client.RequestFilter;
            clone.ResponseFilter = client.ResponseFilter;

            return clone;
        }
    }
}
