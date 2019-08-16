using Sitecore;
using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Data.Managers;
using Sitecore.Jobs;
using Sitecore.Publishing;
using Sitecore.SecurityModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace FortisCollections.Toolcore.Publish
{
    public class Publisher : IPublisher
    {
        public string Publish(IPublisherOptions options)
        {
            using (new SecurityDisabler())
            {
                var publisherOptions = SetDefaults(options);
                var sourceDatabase = Factory.GetDatabase(publisherOptions.SourceDatabaseName);
                var languages = LanguageManager.GetLanguages(sourceDatabase).AsEnumerable();

                if (publisherOptions.LanguageNames != null && publisherOptions.LanguageNames.Length > 0)
                {
                    languages = languages.Where(l => publisherOptions.LanguageNames.Contains(l.Name));
                }

                var targets = PublishManager.GetPublishingTargets(sourceDatabase).AsEnumerable();
                var targetDatabases = new List<Database>();

                if (publisherOptions.TargetNames != null && publisherOptions.TargetNames.Length > 0)
                {
                    targets = targets.Where(t => publisherOptions.TargetNames.Contains(t.Name));
                }

                foreach (var target in targets)
                {
                    targetDatabases.Add(Factory.GetDatabase(target["Target database"]));
                }

                var publishMode = ParsePublishMode(publisherOptions.PublishMode);

                var publishOptions = new PublishOptions(
                    sourceDatabase,
                    targetDatabases.First(),
                    publishMode,
                    languages.First(),
                    DateTime.Now,
                    publisherOptions.TargetNames.ToList())
                {
                    Deep = publisherOptions.Deep,
                    RootItem = sourceDatabase.Items[publisherOptions.RootItem]
                };

                Handle jobHandle = null;
                jobHandle = Sitecore.Publishing.PublishManager.PublishItem(
                    sourceDatabase.Items[publisherOptions.RootItem], targetDatabases.ToArray(),
                    languages.ToArray(), publisherOptions.Deep, true, false);
                // We need to keep a wait
                // as it takes sometime to
                // Trigger job
                // Try for 10 seconds
                // TODO :  Make this configurable

                int sleepTime = 1000; //1000
                int retryCount = 10; // 10
                var publishJobName = "Publish";
                Job job = null;
                for (int i = 0; i <= retryCount; i++)
                {
                    // Sitecore 91 was not giving job name
                    // As it seems it has been changed
                    // So, instead of JobHandle using publish job name
                    job = Sitecore.Jobs.JobManager.GetJob(publishJobName);
                    if (job != null)
                    {
                        break;
                    }
                    else
                    {
                        Thread.Sleep(sleepTime);
                    }
                }
                return job.Name;
            }
        }

        public IPublisherOptions SetDefaults(IPublisherOptions publisherOptions)
        {
            return (new DefaultPublisherOptionsFactory()).Create(publisherOptions);
        }

        public PublishMode ParsePublishMode(string unparsedPublishMode)
        {
            PublishMode publishMode;

            if (!Enum.TryParse(unparsedPublishMode, out publishMode))
            {
                publishMode = PublishMode.Smart;
            }

            return publishMode;
        }
    }
}