﻿using LyricsScraperNET.Models.Requests;
using LyricsScraperNET.Providers.LyricFind;
using LyricsScraperNET.Providers.Models;
using LyricsScraperNET.TestShared.Extensions;
using LyricsScraperNET.TestShared.Providers;
using LyricsScraperNET.UnitTest.TestModel;
using Xunit;

namespace LyricsScraperNET.UnitTest.Providers.LyricFind
{
    public class LyricFindProviderTest : ProviderTestBase
    {
        [Theory]
        [MemberData(nameof(GetTestData), parameters: "Providers\\LyricFind\\test_data.json")]
        public void SearchLyric_UnitDynamicData_AreEqual(LyricsTestData testData)
        {
            // Arrange
            var lyricsClient = new LyricFindProvider();
            lyricsClient.ConfigureExternalProvider(testData);

            SearchRequest searchRequest = CreateSearchRequest(testData);

            // Act
            var searchResult = lyricsClient.SearchLyric(searchRequest);

            // Assert
            Assert.NotNull(searchResult);
            Assert.Equal(ExternalProviderType.LyricFind, searchResult.ExternalProviderType);
            Assert.Equal(testData.LyricResultData.Replace("\r\n", "\n"), searchResult.LyricText.Replace("\r\n", "\n"));
        }
    }
}
