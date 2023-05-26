﻿using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace GPTipsBot.Extensions
{
    public static class RestClientExtensions
    {
        public static async Task<RestResponse?> ExecuteWithRetry(this RestClient restClient, RestRequest request, int maxRetries = 3, CancellationToken cancellationToken = default)
        {
            int retryCount = 0;
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

        public static RestResponse? ExecuteWithPredicate(this RestClient restClient, RestRequest request, Func<RestResponse, int, bool> predicate)
        {
            int retryCount = 0;
            RestResponse response = null;
            //request.Timeout = (int)TimeSpan.FromSeconds(120).TotalMilliseconds;

            while (!predicate(response, retryCount))
            {
                try
                {
                    response = restClient.Execute(request);
                }
                catch (Exception e) {}
                finally
                {
                    retryCount++;
                }
            }

            return response;
        }
    }
}
