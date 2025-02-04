﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace Microsoft.Bot.StreamingExtensions
{
    /// <summary>
    /// Helper methods added to the <see cref="ReceiveRequest"/> class.
    /// </summary>
    public static class ReceiveRequestExtensions
    {
        /// <summary>
        /// Serializes the body of this <see cref="ReceiveRequest"/> as JSON.
        /// </summary>
        /// <typeparam name="T">The type to attempt to deserialize the contents of this <see cref="ReceiveRequest"/>'s body into.</typeparam>
        /// <param name="request">The current instance of <see cref="ReceiveRequest"/>.</param>
        /// <returns>On success, an object of type T populated with data serialized from the <see cref="ReceiveRequest"/> body.
        /// Otherwise a default instance of type T.
        /// </returns>
        public static T ReadBodyAsJson<T>(this ReceiveRequest request)
        {
            // The first stream attached to a ReceiveRequest is always the ReceiveRequest body.
            // Any additional streams must be defined within the body or they will not be
            // attached properly when processing activities.
            try
            {
                var contentStream = request.Streams.FirstOrDefault();
                using (var reader = new StreamReader(contentStream.Stream, Encoding.UTF8))
                {
                    using (var jsonReader = new JsonTextReader(reader))
                    {
                        var serializer = JsonSerializer.Create(SerializationSettings.DefaultDeserializationSettings);
                        return serializer.Deserialize<T>(jsonReader);
                    }
            }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// Reads the body of this <see cref="ReceiveRequest"/> as a string.
        /// </summary>
        /// <param name="request">The current instance of <see cref="ReceiveRequest"/>.</param>
        /// <returns>On success, a string populated with data read from the <see cref="ReceiveRequest"/> body.
        /// Otherwise null.
        /// </returns>
        public static string ReadBodyAsString(this ReceiveRequest request)
        {
            try
            {
                var contentStream = request.Streams.FirstOrDefault();
                using (var reader = new StreamReader(contentStream.Stream, Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}
