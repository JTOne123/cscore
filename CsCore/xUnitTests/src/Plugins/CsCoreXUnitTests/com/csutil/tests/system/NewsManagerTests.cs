using com.csutil.keyvaluestore;
using com.csutil.system;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace com.csutil.tests.system {

    public class NewsManagerTests {

        public NewsManagerTests(Xunit.Abstractions.ITestOutputHelper logger) { logger.UseAsLoggingOutput(); }

        [Fact]
        public async Task ExampleUsage1() {

            // Get your key from https://console.developers.google.com/apis/credentials
            var apiKey = "AIzaSyCtcFQMgRIUHhSuXggm4BtXT4eZvUrBWN0";
            // See https://docs.google.com/spreadsheets/d/1Hwu4ZtRR0iXD65Wuj_XyJxLw4PN8SE0sRgnBKeVoq3A
            var sheetId = "1Hwu4ZtRR0iXD65Wuj_XyJxLw4PN8SE0sRgnBKeVoq3A";
            var sheetName = "MySheet1"; // Has to match the sheet name
            IKeyValueStore newsStore = new GoogleSheetsKeyValueStore(new InMemoryKeyValueStore(), apiKey, sheetId, sheetName);

            var newsLocalDataStore = new InMemoryKeyValueStore().GetTypeAdapter<News.LocalData>();
            NewsManager manager = new NewsManager(newsLocalDataStore, newsStore.GetTypeAdapter<News>());

            IEnumerable<News> allNews = await manager.GetAllNews();
            var news1 = allNews.First();
            Assert.NotNull(news1);
            Assert.Equal("Coming Soon", news1.type);
            Assert.Equal(News.NewsType.ComingSoon, news1.GetNewsType());
            Assert.True(news1.GetDate().IsUtc());

            // Mark that the user has read the news:
            await manager.MarkNewsAsRead(news1);
            Assert.True(allNews.First().localData.isRead);
            Assert.True((await manager.GetAllNews()).First().localData.isRead);
            Assert.True((await newsLocalDataStore.Get(news1.key, null)).isRead);

            IEnumerable<News> unreadNews = await manager.GetAllUnreadNews();
            Assert.Contains(news1, allNews);
            Assert.DoesNotContain(news1, unreadNews);

        }

    }

}