using RestSharp;
using System.Net;

namespace GPTipsBot.Extensions
{
    public static class RestClientExtensions
    {
        public static async Task<RestResponse?> ExecuteWithRetry(this RestClient restClient, RestRequest request, int maxRetries = 3, CancellationToken cancellationToken = default)
        {
            var retryCount = 0;
            RestResponse? response = null;

            while (retryCount < maxRetries)
            {
                try
                {
                    response = await restClient.ExecuteAsync(request, cancellationToken);
                    if (cancellationToken.IsCancellationRequested)
                    {
                        throw new OperationCanceledException();
                    }
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        return response;
                    }
                }
                catch (OperationCanceledException e)
                {
                    throw e;
                }
                catch (Exception e)
                {

                }
                finally {
                    retryCount++;
                }

                if (retryCount >= maxRetries)
                {
                    break;
                }
            }

            return response;
        }

        public static async Task<RestResponse?> ExecuteWithPredicate(this RestClient restClient, RestRequest request, CancellationToken token, Func<RestResponse, int, bool> predicate)
        {
            var retryCount = 0;
            RestResponse response = null;
            //request.Timeout = (int)TimeSpan.FromSeconds(120).TotalMilliseconds;

            while (!predicate(response, retryCount))
            {
                try
                {
                    response = await restClient.ExecuteAsync(request, token);
                    if (response?.ErrorException is OperationCanceledException)
                    {
                        throw response.ErrorException;
                    }
                }
                catch (OperationCanceledException e) {
                    throw;
                }
                catch (Exception e) {

                }
                finally
                {
                    retryCount++;
                }
            }

            return response;
        }
    }
}
