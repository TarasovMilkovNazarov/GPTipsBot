﻿using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace GPTipsBot.Extensions
{
    internal static class RestClientExtensions
    {
        public static RestResponse? ExecuteWithRetry(this RestClient restClient, RestRequest request, int maxRetries = 3)
        {
            int retryCount = 0;
            RestResponse response = null;
            //request.Timeout = (int)TimeSpan.FromSeconds(120).TotalMilliseconds;

            while (retryCount < maxRetries)
            {
                try
                {
                    response = restClient.Execute(request);
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        return response;
                    }
                    
                    retryCount++;
                }
                catch (Exception e)
                {
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
