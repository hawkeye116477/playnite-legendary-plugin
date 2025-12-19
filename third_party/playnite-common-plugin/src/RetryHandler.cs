using Playnite.SDK;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CommonPlugin
{
    public class RetryHandler : DelegatingHandler
    {
        private readonly int _maxRetries = 3;
        private readonly int _baseDelayMs = 500;
        private ILogger logger = LogManager.GetLogger();

        public RetryHandler(HttpMessageHandler innerHandler) : base(innerHandler) { }

        public RetryHandler(HttpMessageHandler innerHandler, int maxRetries, int baseDelayMs) : base(innerHandler)
        {
            _maxRetries = maxRetries;
            _baseDelayMs = baseDelayMs;
        }

        internal static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage originalRequest)
        {
            var newRequest = new HttpRequestMessage(originalRequest.Method, originalRequest.RequestUri)
            {
                Version = originalRequest.Version,
            };

            foreach (var header in originalRequest.Headers)
            {
                newRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            foreach (var property in originalRequest.Properties)
            {
                newRequest.Properties.Add(property);
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
            HttpResponseMessage response = null;
            for (var i = 0; i < _maxRetries; i++)
            {
                try
                {
                    var newRequest = await CloneRequestAsync(request);
                    response = await base.SendAsync(newRequest, token);
                    if ((int)response.StatusCode >= 500 && (int)response.StatusCode < 600)
                    {
                        var errorBody = await response.Content.ReadAsStringAsync();
                        throw new HttpRequestException($"Server error: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {errorBody}");
                    }
                    else
                    {
                        return response;
                    }
                }
                catch when (!token.IsCancellationRequested)
                {
                    if (i < _maxRetries - 1)
                    {
                        int delay = (int)(_baseDelayMs * Math.Pow(2, i));
                        logger.Debug($"Retrying request.... . Attempts left: {_maxRetries - i - 1}");
                        await Task.Delay(delay, token);
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            return response;
        }
    }
}
