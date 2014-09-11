using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.Clients.Nzbget;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Test.Common;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.RemotePathMappings;

namespace NzbDrone.Core.Test.Download.DownloadClientTests.NzbgetTests
{
    [TestFixture]
    public class NzbgetFixture : DownloadClientFixtureBase<Nzbget>
    {
        private NzbgetQueueItem _queued;
        private NzbgetHistoryItem _failed;
        private NzbgetHistoryItem _completed;

        [SetUp]
        public void Setup()
        {
            Subject.Definition = new DownloadClientDefinition();
            Subject.Definition.Settings = new NzbgetSettings
                                          {
                                              Host = "127.0.0.1",
                                              Port = 2222,
                                              Username = "admin",
                                              Password = "pass",
                                              TvCategory = "tv",
                                              RecentTvPriority = (int)NzbgetPriority.High
                                          };

            _queued = new NzbgetQueueItem
                {
                    FileSizeLo = 1000,
                    RemainingSizeLo = 10,
                    Category = "tv",
                    NzbName = "Droned.S01E01.Pilot.1080p.WEB-DL-DRONE",
                    Parameters = new List<NzbgetParameter> { new NzbgetParameter { Name = "drone", Value = "id" } }
                };

            _failed = new NzbgetHistoryItem
                {
                    FileSizeLo = 1000,
                    Category = "tv",
                    Name = "Droned.S01E01.Pilot.1080p.WEB-DL-DRONE",
                    DestDir = "somedirectory",
                    Parameters = new List<NzbgetParameter> { new NzbgetParameter { Name = "drone", Value = "id" } },
                    ParStatus = "Some Error",
                    UnpackStatus = "NONE",
                    MoveStatus = "NONE",
                    ScriptStatus = "NONE",
                    DeleteStatus = "NONE",
                    MarkStatus = "NONE"
                };

            _completed = new NzbgetHistoryItem
                {
                    FileSizeLo = 1000,
                    Category = "tv",
                    Name = "Droned.S01E01.Pilot.1080p.WEB-DL-DRONE",
                    DestDir = "/remote/mount/tv/Droned.S01E01.Pilot.1080p.WEB-DL-DRONE",
                    Parameters = new List<NzbgetParameter> { new NzbgetParameter { Name = "drone", Value = "id" } },
                    ParStatus = "SUCCESS",
                    UnpackStatus = "NONE",
                    MoveStatus = "SUCCESS",
                    ScriptStatus = "NONE",
                    DeleteStatus = "NONE",
                    MarkStatus = "NONE"
                };

            Mocker.GetMock<INzbgetProxy>()
                .Setup(s => s.GetGlobalStatus(It.IsAny<NzbgetSettings>()))
                .Returns(new NzbgetGlobalStatus
                {
                    DownloadRate = 7000000
                });

            var configItems = new Dictionary<String, String>();
            configItems.Add("Category1.Name", "tv");
            configItems.Add("Category1.DestDir", @"/remote/mount/tv");

            Mocker.GetMock<INzbgetProxy>()
                .Setup(v => v.GetConfig(It.IsAny<NzbgetSettings>()))
                .Returns(configItems);
        }

        protected void GivenFailedDownload()
        {
            Mocker.GetMock<INzbgetProxy>()
                .Setup(s => s.DownloadNzb(It.IsAny<Byte[]>(), It.IsAny<String>(), It.IsAny<String>(), It.IsAny<int>(), It.IsAny<NzbgetSettings>()))
                .Returns((String)null);
        }

        protected void GivenSuccessfulDownload()
        {
            Mocker.GetMock<INzbgetProxy>()
                .Setup(s => s.DownloadNzb(It.IsAny<Byte[]>(), It.IsAny<String>(), It.IsAny<String>(), It.IsAny<int>(), It.IsAny<NzbgetSettings>()))
                .Returns(Guid.NewGuid().ToString().Replace("-", ""));
        }

        protected virtual void GivenQueue(NzbgetQueueItem queue)
        {
            var list = new List<NzbgetQueueItem>();

            if (queue != null)
            {
                list.Add(queue);
            }

            Mocker.GetMock<INzbgetProxy>()
                .Setup(s => s.GetQueue(It.IsAny<NzbgetSettings>()))
                .Returns(list);

            Mocker.GetMock<INzbgetProxy>()
                .Setup(s => s.GetPostQueue(It.IsAny<NzbgetSettings>()))
                .Returns(new List<NzbgetPostQueueItem>());
        }

        protected virtual void GivenHistory(NzbgetHistoryItem history)
        {
            var list = new List<NzbgetHistoryItem>();

            if (history != null)
            {
                list.Add(history);
            }

            Mocker.GetMock<INzbgetProxy>()
                .Setup(s => s.GetHistory(It.IsAny<NzbgetSettings>()))
                .Returns(list);
        }

        [Test]
        public void GetItems_should_return_no_items_when_queue_is_empty()
        {
            GivenQueue(null);
            GivenHistory(null);

            Subject.GetItems().Should().BeEmpty();
        }

        [Test]
        public void queued_item_should_have_required_properties()
        {
            _queued.ActiveDownloads = 0;

            GivenQueue(_queued);
            GivenHistory(null);
            
            var result = Subject.GetItems().Single();

            VerifyQueued(result);
        }

        [Test]
        public void paused_item_should_have_required_properties()
        {
            _queued.PausedSizeLo = _queued.RemainingSizeLo;

            GivenQueue(_queued);
            GivenHistory(null);

            var result = Subject.GetItems().Single();

            VerifyPaused(result);
        }

        [Test]
        public void downloading_item_should_have_required_properties()
        {
            _queued.ActiveDownloads = 1;

            GivenQueue(_queued);
            GivenHistory(null);

            var result = Subject.GetItems().Single();

            VerifyDownloading(result);
        }

        [Test]
        public void completed_download_should_have_required_properties()
        {
            GivenQueue(null);
            GivenHistory(_completed);

            var result = Subject.GetItems().Single();

            VerifyCompleted(result);
        }

        [Test]
        public void failed_item_should_have_required_properties()
        {
            GivenQueue(null);
            GivenHistory(_failed);

            var result = Subject.GetItems().Single();

            VerifyFailed(result);
        }

        [Test]
        public void Download_should_return_unique_id()
        {
            GivenSuccessfulDownload();

            var remoteEpisode = CreateRemoteEpisode();

            var id = Subject.Download(remoteEpisode);

            id.Should().NotBeNullOrEmpty();
        }

        [Test]
        public void GetItems_should_ignore_downloads_from_other_categories()
        {
            _completed.Category = "mycat";

            GivenQueue(null);
            GivenHistory(_completed);

            var items = Subject.GetItems();

            items.Should().BeEmpty();
        }

        [Test]
        public void should_return_status_with_outputdir()
        {
            var result = Subject.GetStatus();

            result.IsLocalhost.Should().BeTrue();
            result.OutputRootFolders.Should().NotBeNull();
            result.OutputRootFolders.First().Should().Be(@"/remote/mount/tv");
        }

        [Test]
        public void should_return_status_with_mounted_outputdir()
        {
            Mocker.GetMock<IRemotePathMappingService>()
                .Setup(v => v.RemapRemoteToLocal("127.0.0.1", "/remote/mount/tv"))
                .Returns(@"O:\mymount".AsOsAgnostic());

            var result = Subject.GetStatus();

            result.IsLocalhost.Should().BeTrue();
            result.OutputRootFolders.Should().NotBeNull();
            result.OutputRootFolders.First().Should().Be(@"O:\mymount".AsOsAgnostic());
        }

        [Test]
        public void should_remap_storage_if_mounted()
        {
            Mocker.GetMock<IRemotePathMappingService>()
                .Setup(v => v.RemapRemoteToLocal("127.0.0.1", "/remote/mount/tv/Droned.S01E01.Pilot.1080p.WEB-DL-DRONE"))
                .Returns(@"O:\mymount\Droned.S01E01.Pilot.1080p.WEB-DL-DRONE".AsOsAgnostic());

            GivenQueue(null);
            GivenHistory(_completed);

            var result = Subject.GetItems().Single();

            result.OutputPath.Should().Be(@"O:\mymount\Droned.S01E01.Pilot.1080p.WEB-DL-DRONE".AsOsAgnostic());
        }
    }
}
