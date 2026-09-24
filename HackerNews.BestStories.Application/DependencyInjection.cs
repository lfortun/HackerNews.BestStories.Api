using HackerNews.BestStories.Application.Interfaces;
using HackerNews.BestStories.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace HackerNews.BestStories.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddScoped<IGetBestStoriesQuery, GetBestStoriesQuery>();
            return services;
        }
    }
}
