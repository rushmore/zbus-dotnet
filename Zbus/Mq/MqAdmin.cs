using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Zbus.Mq
{ 
    public class MqAdmin
    {
        public ServerSelector AdminSelector { get; set; }
        public string Token { get; set; }


        protected Broker broker;

        public MqAdmin(Broker broker)
        {
            this.broker = broker;

            AdminSelector = (routeTable, msg) =>
            {
                return routeTable.ServerTable.Keys.ToArray();
            };
        }  
        
        public async Task<ServerInfo[]> QueryServerAsync(CancellationToken? token = null, ServerSelector selector = null)
        {
            Message msg = new Message
            {
                Cmd = Protocol.QUERY,
            };
            return await InvokeObjectAsync<ServerInfo>(msg, token, selector);
        }

        public async Task<TopicInfo[]> QueryTopicAsync(string topic, CancellationToken? token = null, ServerSelector selector = null)
        {
            Message msg = new Message
            {
                Cmd = Protocol.QUERY,
                Topic = topic,
            };
            return await InvokeObjectAsync<TopicInfo>(msg, token, selector);
        }

        public async Task<ConsumeGroupInfo[]> QueryGroupAsync(string topic, string group, CancellationToken? token = null, ServerSelector selector = null)
        {
            Message msg = new Message
            {
                Cmd = Protocol.QUERY,
                Topic = topic,
                ConsumeGroup = group,

            };
            return await InvokeObjectAsync<ConsumeGroupInfo>(msg, token, selector);
        }

        public async Task<TopicInfo[]> DeclareTopicAsync(string topic, int? topicMask = null, CancellationToken? token = null, ServerSelector selector = null)
        {
            Message msg = new Message
            {
                Cmd = Protocol.DECLARE,
                Topic = topic,
                TopicMask = topicMask,
            };
            return await InvokeObjectAsync<TopicInfo>(msg, token, selector);
        }

        public async Task<ConsumeGroupInfo[]> DeclareGroupAsync(string topic, string group, CancellationToken? token = null, ServerSelector selector = null)
        {
            return await DeclareGroupAsync(topic, new ConsumeGroup(group), token, selector);
        }

        public async Task<ConsumeGroupInfo[]> DeclareGroupAsync(string topic, ConsumeGroup group, CancellationToken? token = null, ServerSelector selector = null)
        {
            Message msg = new Message
            {
                Cmd = Protocol.DECLARE,
                Topic = topic,
                ConsumeGroup = group.GroupName,
                GroupFilter = group.Filter,
                GroupMask = group.Mask,
                GroupStartCopy = group.StartCopy,
                GroupStartMsgid = group.StartMsgId,
                GroupStartOffset = group.StartOffset,
                GroupStartTime = group.StartTime,
            };
            return await InvokeObjectAsync<ConsumeGroupInfo>(msg, token, selector);
        }


        public async Task<object[]> RemoveTopicAsync(string topic, CancellationToken? token = null, ServerSelector selector = null)
        {
            Message msg = new Message
            {
                Cmd = Protocol.REMOVE,
                Topic = topic,
            };
            return await CheckedInvokeAsync(msg, token, selector);
        }

        public async Task<object[]> RemoveGroupAsync(string topic, string group, CancellationToken? token = null, ServerSelector selector = null)
        {
            Message msg = new Message
            {
                Cmd = Protocol.REMOVE,
                Topic = topic,
                ConsumeGroup = group,
            };
            return await CheckedInvokeAsync(msg, token, selector);
        }

        public async Task<object[]> EmptyTopicAsync(string topic, CancellationToken? token = null, ServerSelector selector = null)
        {
            Message msg = new Message
            {
                Cmd = Protocol.EMPTY,
                Topic = topic,
            };
            return await CheckedInvokeAsync(msg, token, selector);
        }

        public async Task<object[]> EmptyGroupAsync(string topic, string group, CancellationToken? token = null, ServerSelector selector = null)
        {
            Message msg = new Message
            {
                Cmd = Protocol.EMPTY,
                Topic = topic,
                ConsumeGroup = group,
            };
            return await CheckedInvokeAsync(msg, token, selector);
        }


        public async Task<T[]> InvokeObjectAsync<T>(Message msg, CancellationToken? token = null, ServerSelector selector = null)
            where T : ErrorInfo, new()
        {
            msg.Token = Token;

            if (selector == null)
            {
                selector = AdminSelector;
            }

            MqClientPool[] pools = broker.Select(selector, msg);
            T[] res = new T[pools.Length];
            for (int i = 0; i < pools.Length; i++)
            {
                var pool = pools[i];
                MqClient client = null;
                try
                {
                    client = pool.Borrow();
                    res[i] = await client.InvokeObjectAsync<T>(msg, token);
                }
                finally
                {
                    if (client != null)
                    {
                        pool.Return(client);
                    }
                }

            }
            return res;
        }

        public async Task<object[]> CheckedInvokeAsync(Message msg, CancellationToken? token = null, ServerSelector selector = null)
        {
            msg.Token = Token;

            if (selector == null)
            {
                selector = AdminSelector;
            }

            MqClientPool[] pools = broker.Select(selector, msg);
            object[] res = new object[pools.Length];
            for (int i = 0; i < pools.Length; i++)
            {
                var pool = pools[i];
                MqClient client = null;
                try
                {
                    client = pool.Borrow();
                    await client.CheckedInvokeAsync(msg, token);
                }
                catch (Exception e)
                {
                    res[i] = e;
                }
                finally
                {
                    if (client != null)
                    {
                        pool.Return(client);
                    }
                }

            }
            return res;
        }

    }
}