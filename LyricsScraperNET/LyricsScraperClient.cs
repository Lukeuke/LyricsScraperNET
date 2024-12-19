﻿using LyricsScraperNET.Common;
using LyricsScraperNET.Configuration;
using LyricsScraperNET.Extensions;
using LyricsScraperNET.Helpers;
using LyricsScraperNET.Models.Requests;
using LyricsScraperNET.Models.Responses;
using LyricsScraperNET.Providers;
using LyricsScraperNET.Providers.Abstract;
using LyricsScraperNET.Providers.Models;
using LyricsScraperNET.Validations;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LyricsScraperNET
{
    public sealed class LyricsScraperClient : ILyricsScraperClient
    {
        private ILoggerFactory _loggerFactory;
        private ILogger<LyricsScraperClient> _logger;

        private IProviderService _providerService;
        private IRequestValidator _requestValidator;
        private readonly ILyricScraperClientConfig _lyricScraperClientConfig;

        public bool IsEnabled => _providerService.AnyEnabled();

        public IExternalProvider this[ExternalProviderType providerType]
        {
            get => _providerService[providerType];
        }

        public LyricsScraperClient()
        {
            _providerService = new ProviderService();
            _requestValidator = new RequestValidator();
        }

        public LyricsScraperClient(ILyricScraperClientConfig lyricScraperClientConfig,
            IEnumerable<IExternalProvider> externalProviders) : this()
        {
            Ensure.ArgumentNotNull(lyricScraperClientConfig, nameof(lyricScraperClientConfig));
            _lyricScraperClientConfig = lyricScraperClientConfig;

            Ensure.ArgumentNotNullOrEmptyList(externalProviders, nameof(externalProviders));
            foreach (var externalProvider in externalProviders)
            {
                _providerService.AddProvider(externalProvider);
            }
        }

        public LyricsScraperClient(ILogger<LyricsScraperClient> logger,
            ILyricScraperClientConfig lyricScraperClientConfig,
            IEnumerable<IExternalProvider> externalProviders)
            : this(lyricScraperClientConfig, externalProviders)
        {
            _logger = logger;
        }

        public SearchResult SearchLyric(SearchRequest searchRequest, CancellationToken cancellationToken = default)
        {
            try
            {
                // Run async operation synchronously
                return SearchLyricInternal(searchRequest,
                    (provider, ct) => Task.FromResult(provider.SearchLyric(searchRequest, ct)),
                    cancellationToken).Result;
            }
            catch (AggregateException ex) when (ex.InnerException is OperationCanceledException)
            {
                // Catch AggregateException and throw OperationCanceledException
                throw ex.InnerException;
            }
        }

        public Task<SearchResult> SearchLyricAsync(SearchRequest searchRequest, CancellationToken cancellationToken = default)
            => SearchLyricInternal(searchRequest,
                (provider, ct) => provider.SearchLyricAsync(searchRequest, ct),
                cancellationToken);

        private async Task<SearchResult> SearchLyricInternal(
            SearchRequest searchRequest,
            Func<IExternalProvider, CancellationToken, Task<SearchResult>> searchAction,
            CancellationToken cancellationToken = default)
        {
            if (!ValidSearchRequestAndConfig(searchRequest, out var searchResult))
                return searchResult;

            // Create a linked cancellation token to propagate cancellation
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            foreach (var provider in _providerService.GetAvailableProviders(searchRequest))
            {
                // Check for cancellation before each external provider call
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    // Await the asynchronous search method with the linked cancellation token
                    var result = await searchAction(provider, linkedCts.Token);
                    if (!result.IsEmpty() || result.Instrumental)
                        return result; // Return the result if it is valid
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Log the cancellation and rethrow the exception
                    _logger?.LogInformation("Search operation was canceled.");
                    throw;
                }
                catch (Exception ex)
                {
                    // Log any unexpected errors to prevent the method from crashing
                    _logger?.LogError(ex, "Error during provider search.");
                }
            }

            // No providers found valid results, log the failure and add a message
            searchResult.AddNoDataFoundMessage(Constants.ResponseMessages.NotFoundLyric);
            return searchResult;
        }

        private bool ValidSearchRequestAndConfig(SearchRequest searchRequest, out SearchResult searchResult)
        {
            searchResult = new SearchResult();
            LogLevel logLevel;
            string errorMessage;

            if (!_requestValidator.IsValidSearchRequest(_providerService, searchRequest, out errorMessage, out logLevel))
            {
                _logger?.Log(logLevel, errorMessage);
                searchResult.AddBadRequestMessage(errorMessage);
                return false;
            }

            if (!_requestValidator.IsValidClientConfiguration(_providerService, out errorMessage, out logLevel))
            {
                _logger?.Log(logLevel, errorMessage);
                searchResult.AddNoDataFoundMessage(errorMessage);
                return false;
            }

            return true;
        }

        public void AddProvider(IExternalProvider provider)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }
            if (_loggerFactory != null)
                provider.WithLogger(_loggerFactory);
            _providerService.AddProvider(provider);
        }

        public void RemoveProvider(ExternalProviderType providerType)
        {
            if (providerType.IsNoneProviderType())
                return;

            _providerService.RemoveProvider(providerType);
        }

        public void Enable() => _providerService.EnableAllProviders();

        public void Disable() => _providerService.DisableAllProviders();

        public void WithLogger(ILoggerFactory loggerFactory) => _providerService.WithLogger(loggerFactory);
    }
}