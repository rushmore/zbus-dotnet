using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Zbus.Mq
{
    public class BrokerRouteTable
    {   
        /// <summary>
        /// { TopicName => [TopicInfo] }
        /// </summary>
        public IDictionary<string, IList<TopicInfo>> TopicTable { get; private set; }
        /// <summary>
        /// { TrackerAddress => Vote }
        /// </summary>
        public IDictionary<ServerAddress, Vote> VotesTable { get; private set; }
        /// <summary>
        /// { ServerAddress => ServerInfo }
        /// </summary>
        public IDictionary<ServerAddress, ServerInfo> ServerTable { get; private set; }

        public double VoteFactor { get; set; }

        public BrokerRouteTable()
        {
            VoteFactor = 0.5;
            VotesTable = new ConcurrentDictionary<ServerAddress, Vote>();
            TopicTable = new Dictionary<string, IList<TopicInfo>>();
            ServerTable = new Dictionary<ServerAddress, ServerInfo>(); 
        }

        public IList<ServerAddress> UpdateTracker(TrackerInfo trackerInfo)
        {
            //1) Update Votes
            ServerAddress trackerAddress = trackerInfo.ServerAddress;
            Vote vote;
            VotesTable.TryGetValue(trackerAddress, out vote);
            if(vote != null && vote.Version >= trackerInfo.InfoVersion)
            {
                return new List<ServerAddress>();
            } 

            ISet<ServerAddress> servers = new HashSet<ServerAddress>();
            foreach(ServerInfo serverInfo in trackerInfo.ServerTable.Values)
            {
                servers.Add(serverInfo.ServerAddress);
            }
            if(vote == null)
            {
                vote = new Vote
                { 
                    Servers = servers,
                };
                VotesTable[trackerAddress] = vote;
            }
            vote.Version = trackerInfo.InfoVersion;
            vote.Servers = servers;

            //2) Merge ServerTable
            foreach (ServerInfo serverInfo in trackerInfo.ServerTable.Values)
            {
                ServerInfo oldServerInfo;
                ServerTable.TryGetValue(serverInfo.ServerAddress, out oldServerInfo);
                if(oldServerInfo != null && oldServerInfo.InfoVersion >= serverInfo.InfoVersion)
                {
                    continue;
                }
                ServerTable[serverInfo.ServerAddress] = serverInfo; 
            }
             
            //3) Purge and Rebuid TopicTable
            return Purge();
        }

        public IList<ServerAddress> RemoveTracker(ServerAddress trackerAddress)
        {
            VotesTable.Remove(trackerAddress);

            return Purge();
        }

        private IList<ServerAddress> Purge()
        { 
            IList<ServerAddress> toRemove = new List<ServerAddress>(); 
            IDictionary<ServerAddress, ServerInfo> serverTableLocal = new Dictionary<ServerAddress, ServerInfo>(ServerTable);

            foreach (ServerInfo serverInfo in ServerTable.Values)
            {
                int count = 0;
                foreach(Vote vote in VotesTable.Values)
                {
                    if (vote.Servers.Contains(serverInfo.ServerAddress))
                    {
                        count++;
                    } 
                }
                if(count < VotesTable.Count * VoteFactor)
                {
                    toRemove.Add(serverInfo.ServerAddress);
                    serverTableLocal.Remove(serverInfo.ServerAddress);
                }
            }  

            ServerTable = serverTableLocal;
            RebuildTopicTable(); 

            return toRemove;
        } 

        private void RebuildTopicTable()
        {
            var localTopicTable = new ConcurrentDictionary<string, IList<TopicInfo>>();
            foreach(ServerInfo serverInfo in ServerTable.Values)
            { 
                foreach(TopicInfo topicInfo in serverInfo.TopicTable.Values)
                { 
                    IList<TopicInfo> topicServerList;
                    localTopicTable.TryGetValue(topicInfo.TopicName, out topicServerList);
                    if (topicServerList == null)
                    {
                        topicServerList = new List<TopicInfo>();
                        localTopicTable[topicInfo.TopicName] = topicServerList;
                    }   
                    topicServerList.Add(topicInfo);
                }
            }
            TopicTable = localTopicTable;
        }

        public class Vote
        {
            public long Version { get; set; }
            public ISet<ServerAddress> Servers { get; set; }
        }
    }
}
