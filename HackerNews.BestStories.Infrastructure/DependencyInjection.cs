using HackerNews.BestStories.Application.Interfaces;
using HackerNews.BestStories.Application.Options;
using HackerNews.BestStories.Infrastructure.Cache;
using HackerNews.BestStories.Infrastructure.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;

namespace HackerNews.BestStories.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            // 1. Register native in-memory cache services
            services.AddMemoryCache();

            // 2. Register strongly typed options
            services.Configure<HackerNewsOptions>(
                configuration.GetSection(HackerNewsOptions.SectionName));

            // 3. Configure the HttpClient for the CONCRETE class (HackerNewsClient)
            // This keeps the benefits of HttpClientFactory without assigning it directly to the interface yet.
            services.AddHttpClient<HackerNewsClient>((serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<HackerNewsOptions>>().Value;

                client.BaseAddress = new Uri(options.BaseUrl);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.Timeout = TimeSpan.FromSeconds(options.Timeout);
            })
            .AddPolicyHandler(GetRetryPolicy());

            // 4. Register the Interface pointing to the DECORATOR
            // The decorator receives the concrete class injected through the HttpClientFactory above.
            services.AddScoped<IHackerNewsClient>(provider =>
                new HackerNewsCacheDecorator(
                    provider.GetRequiredService<HackerNewsClient>(), //Get the real HTTP instance correctly
                    provider.GetRequiredService<IMemoryCache>(),
                    provider.GetRequiredService<ILogger<HackerNewsCacheDecorator>>()
                ));

            return services;
        }

        /// <summary>
        /// Define an exponential retry policy with Polly for transient HTTP failures (5xx, 408, or network failures).
        /// </summary>
        private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()// Catch 5xx errors or status codes like 408 (Timeout)
                .WaitAndRetryAsync(3, retryAttempt =>
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));// Retries at: 2s, 4s, and 8s progressively
        }
    }
}
