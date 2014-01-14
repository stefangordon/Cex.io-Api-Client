﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Nextmethod.Cex
{
    internal static class ApiHelpers
    {

        internal static HttpClient NewHttpClient(this ICexClient This)
        {
            var client = HttpClientFactory.Get();
            client.MaxResponseContentBufferSize = Int32.MaxValue;

            if (This.Timeout != null)
            {
                client.Timeout = This.Timeout.Value;
            }

            return client;
        }

        internal static Uri GetApiUri(this ICexClient This, string path)
        {
            var baseUri = This.BasePathFactory().AbsoluteUri;
            if (!baseUri.EndsWith("/"))
                baseUri += "/";

            if (path.StartsWith("/"))
                path = path.Substring(1);

            return new Uri(string.Concat(baseUri, path));
        }

        [DebuggerHidden]
        internal static async Task<T> GetFromService<T>(this ICexClient This, string servicePath, Func<dynamic, T> resultFactory, CancellationTokenSource tokenSource = null)
        {
            tokenSource = tokenSource ?? new CancellationTokenSource();
            var uri = This.GetApiUri(servicePath);
            using (var client = This.NewHttpClient())
            {
                using (var response = await client.GetAsync(uri, tokenSource.Token))
                {
                    var body = await response.Content.ReadAsStringAsync();
                    dynamic json = SimpleJson.DeserializeObject(body);

                    ExceptionOracle.ThrowIfError(response, json);
                    return resultFactory(json);
                }
            }
        }

        [DebuggerHidden]
        internal static async Task<T> PostToService<T>(this ICexClient This, string servicePath, Func<IEnumerable<KeyValuePair<string, string>>> paramFactory, Func<dynamic, T> resultFactory, CancellationTokenSource tokenSource = null)
        {
            tokenSource = tokenSource ?? new CancellationTokenSource();

            long nonce;
            var signature = This.Credentials.NewSignature(out nonce);
            var content = new FormUrlEncodedContent(
                new[]
                {
                    NewRequestParam(This, Constants.ApiParamKey, This.Credentials.ApiKey),
                    NewRequestParam(This, Constants.ApiParamSignature, signature),
                    NewRequestParam(This, Constants.ApiParamNonce, Convert.ToString(nonce))
                }
                    .Concat(paramFactory())
                );

            var uri = This.GetApiUri(servicePath);
            using (var client = This.NewHttpClient())
            {
                using (var response = await client.PostAsync(uri, content, tokenSource.Token))
                {
                    var body = await response.Content.ReadAsStringAsync();
                    dynamic json = SimpleJson.DeserializeObject(body);

                    ExceptionOracle.ThrowIfError(response, json);
                    return resultFactory(json);
                }
            }
        }

        internal static KeyValuePair<string, string> NewRequestParam(this ICexClient This, string key, string value)
        {
            return new KeyValuePair<string, string>(key, value);
        }

    }
}
