// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
// Generated with Bot Builder V4 SDK Template for Visual Studio EchoBot v4.3.0

using Microsoft.Bot.Builder.Adapters.Webex;

namespace Microsoft.Bot.Builder.Webex.Sample
{
    public class SimpleWebexAdapterOptions : IWebexAdapterOptions
    {
        public SimpleWebexAdapterOptions(string accessToken, string publicAdress, string secret)
        {
            this.AccessToken = accessToken;
            this.PublicAdress = publicAdress;
            this.Secret = secret;
        }

        public string AccessToken { get; set; }

        public string PublicAdress { get; set; }

        public string Secret { get; set; }

        public string WebhookName { get; set; }
    }
}
