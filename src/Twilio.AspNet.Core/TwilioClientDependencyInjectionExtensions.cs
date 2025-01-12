using System;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Twilio.Clients;
using Twilio.Http;

namespace Twilio.AspNet.Core
{
    public static class TwilioClientDependencyInjectionExtensions
    {
        internal const string TwilioHttpClientName = "Twilio";

        public static IServiceCollection AddTwilioClient(this IServiceCollection services)
            => AddTwilioClient(services, null, null);

        public static IServiceCollection AddTwilioClient(
            this IServiceCollection services,
            Action<IServiceProvider, TwilioClientOptions> configureTwilioClientOptions
        )
            => AddTwilioClient(services, configureTwilioClientOptions, null);

        public static IServiceCollection AddTwilioClient(
            this IServiceCollection services,
            Func<IServiceProvider, System.Net.Http.HttpClient> provideHttpClient
        )
            => AddTwilioClient(services, null, provideHttpClient);

        public static IServiceCollection AddTwilioClient(
            this IServiceCollection services,
            Action<IServiceProvider, TwilioClientOptions> configureTwilioClientOptions,
            Func<IServiceProvider, System.Net.Http.HttpClient> provideHttpClient
        )
        {
            if (configureTwilioClientOptions == null)
                configureTwilioClientOptions = ConfigureDefaultTwilioClientOptions;

            services.AddOptions<TwilioClientOptions>()
                .Configure<IServiceProvider>((options, serviceProvider) =>
                {
                    configureTwilioClientOptions(serviceProvider, options);
                    SanitizeTwilioClientOptions(options);
                });

            if (provideHttpClient == null)
            {
                provideHttpClient = ProvideDefaultHttpClient;

                services.AddHttpClient(TwilioHttpClientName)
                    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                    {
                        // same options as the Twilio C# SDK
                        AllowAutoRedirect = false
                    });
            }

            services.AddScoped<ITwilioRestClient>(provider => CreateTwilioClient(provider, provideHttpClient));
            services.AddScoped<TwilioRestClient>(provider => CreateTwilioClient(provider, provideHttpClient));

            return services;
        }

        private static void ConfigureDefaultTwilioClientOptions(
            IServiceProvider serviceProvider,
            TwilioClientOptions options
        )
        {
            var configuration = serviceProvider.GetService<IConfiguration>();
            if (configuration == null)
            {
                throw new Exception("IConfiguration not found.");
            }

            var section = configuration.GetSection("Twilio:Client");
            if (section.Exists() == false)
            {
                throw new Exception("Twilio:Client not configured.");
            }

            section.Bind(options);

            // if Twilio:Client:AuthToken is not set, fallback on Twilio:AuthToken
            if (string.IsNullOrEmpty(options.AuthToken)) options.AuthToken = configuration["Twilio:AuthToken"];
        }

        private static void SanitizeTwilioClientOptions(TwilioClientOptions options)
        {
            // properties can be empty strings, but should be set to null if so
            if (options.AccountSid == "") options.AccountSid = null;
            if (options.AuthToken == "") options.AuthToken = null;
            if (options.ApiKeySid == "") options.ApiKeySid = null;
            if (options.ApiKeySecret == "") options.ApiKeySecret = null;
            if (options.Region == "") options.Region = null;
            if (options.Edge == "") options.Edge = null;
            if (options.LogLevel == "") options.LogLevel = null;

            var isApiKeyConfigured = options.AccountSid != null &&
                                     options.ApiKeySid != null &&
                                     options.ApiKeySecret != null;
            var isAuthTokenConfigured = options.AccountSid != null &&
                                        options.AuthToken != null;

            if (options.CredentialType == CredentialType.Unspecified)
            {
                if (isApiKeyConfigured) options.CredentialType = CredentialType.ApiKey;
                else if (isAuthTokenConfigured) options.CredentialType = CredentialType.AuthToken;
                else
                    throw new Exception(
                        "Twilio:Client:CredentialType could not be determined. Configure as ApiKey or AuthToken.");
            }
            else if (options.CredentialType == CredentialType.ApiKey && !isApiKeyConfigured)
            {
                throw new Exception(
                    "Twilio:Client:{AccountSid|ApiKeySid|ApiKeySecret} configuration required for CredentialType.ApiKey.");
            }
            else if (options.CredentialType == CredentialType.AuthToken && !isAuthTokenConfigured)
            {
                throw new Exception(
                    "Twilio:Client:{AccountSid|AuthToken} configuration required for CredentialType.AuthToken.");
            }
        }

        private static System.Net.Http.HttpClient ProvideDefaultHttpClient(IServiceProvider serviceProvider)
            => serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(TwilioHttpClientName);

        private static TwilioRestClient CreateTwilioClient(
            IServiceProvider provider,
            Func<IServiceProvider, System.Net.Http.HttpClient> provideHttpClient
        )
        {
            Twilio.Http.HttpClient twilioHttpClient = null;
            if (provideHttpClient != null)
            {
                var httpClient = provideHttpClient(provider);
                twilioHttpClient = new SystemNetHttpClient(httpClient);
            }

            var options = provider.GetRequiredService<IOptions<TwilioClientOptions>>().Value;

            TwilioRestClient client;
            switch (options.CredentialType)
            {
                case CredentialType.ApiKey:
                    client = new TwilioRestClient(
                        username: options.ApiKeySid,
                        password: options.ApiKeySecret,
                        accountSid: options.AccountSid,
                        region: options.Region,
                        httpClient: twilioHttpClient,
                        edge: options.Edge
                    );
                    break;
                case CredentialType.AuthToken:
                    client = new TwilioRestClient(
                        username: options.AccountSid,
                        password: options.AuthToken,
                        accountSid: options.AccountSid,
                        region: options.Region,
                        httpClient: twilioHttpClient,
                        edge: options.Edge
                    );
                    break;
                default:
                    throw new Exception("This code should be unreachable");
            }

            if (options.LogLevel != null)
            {
                client.LogLevel = options.LogLevel;
            }

            return client;
        }
    }
}