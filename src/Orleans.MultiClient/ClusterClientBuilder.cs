﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orleans.Configuration;
using Orleans.Runtime;
using System;
using System.Threading.Tasks;

namespace Orleans.MultiClient
{
    public class ClusterClientBuilder : IClusterClientBuilder
    {
        private readonly OrleansClientOptions _options;
        private readonly ILogger _logger;

        public ClusterClientBuilder(IServiceProvider serviceProvider, OrleansClientOptions options)
        {
            this._logger = serviceProvider.GetRequiredService<ILogger<ClusterClientBuilder>>();
            this._options = options;
        }
        public IClusterClient Build()
        {
            IClientBuilder build = new ClientBuilder();
            if (_options.Configure == null)
            {
                _logger.LogError($"{_options.ServiceName} There is no way to connect to Orleans, please configure it in OrleansClientOptions.Configure");
            }
            _options.Configure(build);
            build.Configure<ClusterOptions>(opt =>
            {
                if (!string.IsNullOrEmpty(_options.ClusterId))
                    opt.ClusterId = _options.ClusterId;
                if (!string.IsNullOrEmpty(_options.ServiceId))
                    opt.ServiceId = _options.ServiceId;
            });

            var client = build.Build();
            return this.ConnectClient(_options.ServiceName, client);
        }

        private IClusterClient ConnectClient(string serviceName, IClusterClient client)
        {
            try
            {
                client.Connect(RetryFilter).Wait();
                _logger.LogDebug($"Connection {serviceName} Sucess...");
                return client;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Connection {serviceName} Faile...", ex);
                throw new Exception($"Connection {serviceName} Faile...");
            }
        }

        private int attempt = 0;
        private async Task<bool> RetryFilter(Exception exception)
        {
            if (exception.GetType() != typeof(SiloUnavailableException))
            {
                _logger.LogError($"Cluster client failed to connect to cluster with unexpected error.  Exception: {exception}");
                return false;
            }
            attempt++;
            _logger.LogError($"Cluster client attempt {attempt} of {10} failed to connect to cluster.  Exception: {exception}");
            if (attempt > 10)
            {
                return false;
            }
            await Task.Delay(TimeSpan.FromSeconds(4));
            return true;
        }
    }
}