using Playnite;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CommonPlugin
{
    public class RetryHandler : DelegatingHandler
    {
        private readonly int maxRetries = 3;
        private readonly int baseDelayMs = 500;
        private ILogger logger = LogManager.GetLogger();

        public RetryHandler(HttpMessageHandler innerHandler) : base(innerHandler) { }

        public RetryHandler(HttpMessageHandler innerHandler, int maxRetries, int baseDelayMs) : base(innerHandler)
        {
            this.maxRetries = maxRetries;
            this.baseDelayMs = baseDelayMs;
        }

        private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage originalRequest)
        {
            var newRequest = new HttpRequestMessage(originalRequest.Method, originalRequest.RequestUri)
            {
                Version = originalRequest.Version,
            };

            foreach (var header in originalRequest.Headers)
            {
                newRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            
            foreach (var option in originalRequest.Options)
            {
                newRequest.Options.TryAdd(option.Key, option.Value);
            }

            if (originalRequest.Content != null)
            {
                var contentBytes = await originalRequest.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                var newContent = new ByteArrayContent(contentBytes);
                foreach (var contentHeader in originalRequest.Content.Headers)
                {
                    newContent.Headers.TryAddWithoutValidation(contentHeader.Key, contentHeader.Value);
                }
                newRequest.Content = newContent;
            }
            return newRequest;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken token)
        {
            HttpResponseMessage? response = null;
            for (var i = 0; i < maxRetries; i++)
            {
                try
                {
                    var newRequest = await CloneRequestAsync(request);
                    response = await base.SendAsync(newRequest, token);
                    if ((int)response.StatusCode >= 500 && (int)response.StatusCode < 600)
                    {
                        var errorBody = await response.Content.ReadAsStringAsync(token);
                        throw new HttpRequestException($"Server error: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {errorBody}");
                    }
                    return response;
                }
                catch when (!token.IsCancellationRequested)
                {
                    if (i < maxRetries - 1)
                    {
                        int delay = (int)(baseDelayMs * Math.Pow(2, i));
                        logger.Debug($"Retrying request.... . Attempts left: {maxRetries - i - 1}");
                        await Task.Delay(delay, token);
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            if (response == null)
            {
                return new HttpResponseMessage()
                {
                    ReasonPhrase = "Response was null",
                    RequestMessage = request
                };
            }
            return response;
        }
    }
}
